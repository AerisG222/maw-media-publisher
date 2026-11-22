using CliWrap;
using MawMediaPublisher.Metadata;
using MawMediaPublisher.Models;

namespace MawMediaPublisher.Scale;

class PhotoScaler
{
    OutputInspector _inspector = new();

    public async Task<List<ScaledFile>> Scale(
        Category category,
        FileInfo src,
        string baseDir,
        IEnumerable<ScaleSpec> scales
    )
    {
        var dstFilename = $"{Path.GetFileNameWithoutExtension(src.Name)}.avif";
        var targets = scales
            .Select(scale => new ScaleTarget(scale, new FileInfo(Path.Combine(baseDir, scale.Code, dstFilename))))
            .ToList();

        foreach (var target in targets)
        {
            Directory.CreateDirectory(target.Dst.DirectoryName!);
        }

        // see if they exist as we may be trying to reprocess
        var missing = targets.Where(t => !t.Dst.Exists).ToList();

        if (missing.Count > 0)
        {
            await ScaleImage(src, missing);
        }

        var info = await _inspector.InspectWithRetry(targets, t => t.Dst, invalid => ScaleImage(src, invalid));
        var results = new List<ScaledFile>();

        foreach (var target in targets)
        {
            var dims = info[target.Dst.FullName];

            target.Dst.Refresh();

            results.Add(new ScaledFile(
                Guid.CreateVersion7(),
                target.Scale,
                category.BuildMediaFilePath(target.Scale, target.Dst.Name),
                dims.Width,
                dims.Height,
                target.Dst.Length
            ));
        }

        return results;
    }

    static async Task ScaleImage(FileInfo src, IReadOnlyList<ScaleTarget> targets)
    {
        if (RecognizedExtensions.IsRaw(src.Name))
        {
            var tif = $"{src.FullName}.tif";

            if (!Path.Exists(tif))
            {
                await Cli
                    .Wrap("rawtherapee-cli")
                    .WithArguments(GetRawTherapeeArgs(src.FullName, tif))
                    .ExecuteWithErrorsAsync();
            }

            src = new FileInfo(tif);
        }

        await AvifWriter.Write(src.FullName, targets, AvifWriter.PHOTO_QUALITY);
    }

    static IEnumerable<string> GetRawTherapeeArgs(string src, string dstTif)
    {
        return [
            "-d",  // default profile
            "-s",  // sidecar pp3 profile (if exists)
            "-t",  // tif output
            "-o", dstTif,
            "-c", src
        ];
    }
}
