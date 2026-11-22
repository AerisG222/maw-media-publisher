using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;

namespace MawMediaPublisher.Metadata;

class ExifExporter
{
    public async Task<ExifInfo> Export(FileInfo file)
    {
        return await ExtractExif(file);
    }

    async Task<ExifInfo> ExtractExif(FileInfo file)
    {
        using var cmd = Cli
            .Wrap("exiftool")
            .WithArguments([
                "-json",
                "-quiet",
                "-groupHeadings",
                "-long",
                // quicktime (mp4 / mov) dates are stored in UTC, while photo exif dates are local.  this has exiftool
                // convert quicktime dates to local time (w/ an offset) so videos and photos sort correctly together
                "-api", "QuickTimeUTC",
                file.FullName
            ])
            // exiftool returns non-zero when it can't read a file, but reports why in the json (see GetError)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        var cmdResult = await cmd;

        if (string.IsNullOrWhiteSpace(cmdResult.StandardOutput))
        {
            throw new InvalidDataException($"Unable to read json from exiftool for: {file.FullName} (exit code {cmdResult.ExitCode}): {CommandExtensions.Tail(cmdResult.StandardError)}");
        }

        var exif = JsonDocument.Parse(cmdResult.StandardOutput);

        if (exif == null)
        {
            throw new ApplicationException($"Unable to parse exif data for: {file.FullName}");
        }

        if (exif.RootElement.ValueKind == JsonValueKind.Array)
        {
            if (exif.RootElement.EnumerateArray().Count() != 1)
            {
                throw new ApplicationException($"Only expected a single result when parsing exif for: {file.FullName}.  Count was: {exif.RootElement.EnumerateArray().Count()}");
            }

            var element = exif.RootElement.EnumerateArray().First();

            ThrowIfError(file, element);

            return new ExifInfo(element);
        }

        if (exif.RootElement.ValueKind == JsonValueKind.Object)
        {
            ThrowIfError(file, exif.RootElement);

            return new ExifInfo(exif.RootElement);
        }

        throw new ApplicationException($"Did not find expected json result when parsing exif for: {file.FullName}");
    }

    // when exiftool can't read a file (ex: corrupt / not really the type its extension says), it reports this as
    // ExifTool:Error rather than on stderr
    static void ThrowIfError(FileInfo file, JsonElement element)
    {
        if (
            element.TryGetProperty("ExifTool", out var group) &&
            group.ValueKind == JsonValueKind.Object &&
            group.TryGetProperty("Error", out var error)
        )
        {
            var message = error.ValueKind == JsonValueKind.Object && error.TryGetProperty("val", out var val)
                ? val.ToString()
                : error.ToString();

            throw new InvalidDataException($"exiftool could not read {file.Name}: {message}");
        }
    }
}
