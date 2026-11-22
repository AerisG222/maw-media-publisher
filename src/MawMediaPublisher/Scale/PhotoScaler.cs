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
                using var rt = Cli
                    .Wrap("rawtherapee-cli")
                    .WithArguments(GetRawTherapeeArgs(src.FullName, tif))
                    .ExecuteAsync();

                await rt;
            }

            src = new FileInfo(tif);
        }

        using var cmd = Cli
            .Wrap("magick")
            .WithArguments(GetImageMagickArgs(src.FullName, targets))
            .ExecuteAsync();

        await cmd;
    }

    // https://usage.imagemagick.org/resize/
    // https://usage.imagemagick.org/files/#mpr
    //
    // all scales are produced from a single magick process so the (potentially very large) source is only decoded
    // once.  the source is kept in memory (mpr:orig) for the 'full' size, and a linear RGB copy (mpr:lin) is kept
    // for resizing, then each scale is resized / written from that copy inside its own parentheses.  output is
    // identical to running magick separately for each scale.
    static IEnumerable<string> GetImageMagickArgs(string src, IReadOnlyList<ScaleTarget> targets)
    {
        var full = targets.Where(t => t.Scale.Width == int.MaxValue).ToList();
        var resized = targets.Where(t => t.Scale.Width != int.MaxValue).ToList();

        List<string> args = [
            "-respect-parentheses",
            src,
            "-write", "mpr:orig"
        ];

        if (resized.Count > 0)
        {
            // interesting, if we include this for the 'full' size, magick consumes a lot of mem and crashes the
            // process.  looks like magick doesn't like 2 colorspace cmds side by side, so 'full' uses mpr:orig
            args.AddRange([
                "-colorspace", "RGB",
                "-write", "mpr:lin"
            ]);
        }

        args.Add("+delete");

        foreach (var target in resized)
        {
            args.AddRange(["(", "mpr:lin"]);

            if (target.Scale.IsCropToFill)
            {
                args.AddRange([
                    "-resize", $"{target.Scale.Width}x{target.Scale.Height}^",
                    "-gravity", "center",
                    "-crop", $"{target.Scale.Width}x{target.Scale.Height}+0+0"
                ]);
            }
            else
            {
                // the > at the end means "only scale down, not up"
                args.AddRange([
                    "-resize", $"{target.Scale.Width}x{target.Scale.Height}>"
                ]);
            }

            args.AddRange(GetAvifArgs(target.Dst.FullName));
            args.AddRange([")", "+delete"]);
        }

        foreach (var target in full)
        {
            args.AddRange(["(", "mpr:orig"]);
            args.AddRange(GetAvifArgs(target.Dst.FullName));
            args.AddRange([")", "+delete"]);
        }

        // everything has been written above, so there is nothing left for the final output
        args.Add("null:");

        return args;
    }

    static IEnumerable<string> GetAvifArgs(string dst) => [
        "-colorspace", "sRGB",
        "-depth", "8",
        "-define", "heic:chroma=420",
        "-quality", "72",
        "-strip",
        "-write", dst
    ];

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

    record ScaleTarget(ScaleSpec Scale, FileInfo Dst);
}
