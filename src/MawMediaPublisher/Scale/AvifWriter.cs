using CliWrap;

namespace MawMediaPublisher.Scale;

// writes all requested scales of an image to avif using a single magick process.  used for photos and video posters.
static class AvifWriter
{
    // photos: tested against avifenc (default + tune=iq), slower encoder speeds, and 4:4:4 using ssimulacra2 - none
    // were meaningfully better at the same size, so we stick w/ this.  8bit only as android 17 was displaying corrupt
    // images at higher depths.
    public const int PHOTO_QUALITY = 72;

    // posters: q62 was compared to the previous svt-av1 (crf 35) posters - ssimulacra2 went from ~48-67 to ~62-79
    // for ~40-45% larger posters (~90-100KB more per 1080p video, all poster files combined)
    public const int POSTER_QUALITY = 62;

    public static async Task Write(string src, IReadOnlyList<ScaleTarget> targets, int quality)
    {
        await Cli
            .Wrap("magick")
            .WithArguments(GetImageMagickArgs(src, targets, quality))
            .ExecuteWithErrorsAsync();
    }

    // https://usage.imagemagick.org/resize/
    // https://usage.imagemagick.org/files/#mpr
    //
    // all scales are produced from a single magick process so the (potentially very large) source is only decoded
    // once.  the source is kept in memory (mpr:orig) for the 'full' size, and a linear RGB copy (mpr:lin) is kept
    // for resizing, then each scale is resized / written from that copy inside its own parentheses.  output is
    // identical to running magick separately for each scale.
    static IEnumerable<string> GetImageMagickArgs(string src, IReadOnlyList<ScaleTarget> targets, int quality)
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

            args.AddRange(GetAvifArgs(target.Dst.FullName, quality));
            args.AddRange([")", "+delete"]);
        }

        foreach (var target in full)
        {
            args.AddRange(["(", "mpr:orig"]);
            args.AddRange(GetAvifArgs(target.Dst.FullName, quality));
            args.AddRange([")", "+delete"]);
        }

        // everything has been written above, so there is nothing left for the final output
        args.Add("null:");

        return args;
    }

    static IEnumerable<string> GetAvifArgs(string dst, int quality) => [
        "-colorspace", "sRGB",
        "-depth", "8",
        "-define", "heic:chroma=420",
        "-quality", $"{quality}",
        "-strip",
        "-write", dst
    ];
}
