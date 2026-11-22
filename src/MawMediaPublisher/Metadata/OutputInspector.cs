using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;

namespace MawMediaPublisher.Metadata;

record OutputInfo(int Width, int Height);

// inspects the files we produce (scaled photos / videos / posters) to get their dimensions and make sure they are
// valid.  a single exiftool process is used for all of the outputs for a media item, rather than one per file, and
// -validate is used so truncated / partially written files are caught (exiftool will otherwise happily read the
// dimensions from the header of a truncated file).
class OutputInspector
{
    public async Task<Dictionary<string, OutputInfo>> InspectWithRetry<T>(
        IReadOnlyList<T> items,
        Func<T, FileInfo> getFile,
        Func<IReadOnlyList<T>, Task> rebuild
    )
    {
        var info = await Inspect(items.Select(getFile));
        var invalid = items.Where(i => !info.ContainsKey(getFile(i).FullName)).ToList();

        if (invalid.Count == 0)
        {
            return info;
        }

        // if the file existed, it was corrupt, so lets try again
        foreach (var item in invalid)
        {
            getFile(item).Delete();
        }

        await rebuild(invalid);

        var retry = await Inspect(invalid.Select(getFile));

        foreach (var item in invalid)
        {
            var file = getFile(item).FullName;

            if (!retry.TryGetValue(file, out var retryInfo))
            {
                throw new ApplicationException($"Unable to produce a valid file: {file}");
            }

            info[file] = retryInfo;
        }

        return info;
    }

    // returns entries only for files that exist, are valid, and have dimensions
    public async Task<Dictionary<string, OutputInfo>> Inspect(IEnumerable<FileInfo> files)
    {
        var valid = new Dictionary<string, OutputInfo>();
        var paths = files.Select(f => f.FullName).ToList();

        if (paths.Count == 0)
        {
            return valid;
        }

        // exiftool returns non-zero if any file could not be read (ex: missing), but still reports the others
        using var cmd = Cli
            .Wrap("exiftool")
            .WithArguments([
                "-json",
                "-n",
                "-ImageWidth",
                "-ImageHeight",
                "-Validate",
                "-Warning",
                "-Error",
                .. paths
            ])
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        var result = await cmd;

        if (string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return valid;
        }

        using var json = JsonDocument.Parse(result.StandardOutput);

        foreach (var item in json.RootElement.EnumerateArray())
        {
            var path = item.TryGetProperty("SourceFile", out var src) ? src.GetString() : null;

            if (path == null || !IsValid(item))
            {
                continue;
            }

            if (TryGetInt(item, "ImageWidth", out var width) && TryGetInt(item, "ImageHeight", out var height))
            {
                valid[Path.GetFullPath(path)] = new OutputInfo(width, height);
            }
        }

        return valid;
    }

    static bool IsValid(JsonElement item)
    {
        if (item.TryGetProperty("Error", out _))
        {
            return false;
        }

        // only treat truncation warnings as fatal, other warnings may be benign
        return !(
            item.TryGetProperty("Warning", out var warning) &&
            warning.ToString().Contains("truncated", StringComparison.OrdinalIgnoreCase)
        );
    }

    static bool TryGetInt(JsonElement item, string name, out int value)
    {
        value = 0;

        return item.TryGetProperty(name, out var prop) &&
            prop.ValueKind == JsonValueKind.Number &&
            prop.TryGetInt32(out value) &&
            value > 0;
    }
}
