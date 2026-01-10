using CliWrap;
using MawMediaPublisher.Metadata;
using MawMediaPublisher.Models;

namespace MawMediaPublisher.Scale;

class PhotoScaler
{
    ExifExporter _exifExporter = new();

    public async Task<ScaledFile> Scale(
        Category category,
        FileInfo src,
        string dstDir,
        ScaleSpec scale
    )
    {
        var dstFilename = $"{Path.GetFileNameWithoutExtension(src.Name)}.avif";
        var dst = new FileInfo(Path.Combine(dstDir, dstFilename));
        ExifInfo? exif;

        // see if it exists as we may be trying to reprocess
        if (!dst.Exists)
        {
            await ScaleImage(src, dst, scale);
        }

        try
        {
            exif = await _exifExporter.Export(dst);
        }
        catch
        {
            // if file existed, it was corrput, so lets try again
            File.Delete(dst.FullName);
            await ScaleImage(src, dst, scale);
            exif = await _exifExporter.Export(dst);
        }

        dst.Refresh();

        return new ScaledFile(
            Guid.CreateVersion7(),
            scale,
            category.BuildMediaFilePath(scale, dst.Name),
            exif.Width,
            exif.Height,
            dst.Length
        );
    }

    static async Task ScaleImage(FileInfo src, FileInfo dst, ScaleSpec scale)
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
            .WithArguments(GetImageMagickArgs(src.FullName, dst.FullName, scale))
            .ExecuteAsync();

        await cmd;
    }

    // https://usage.imagemagick.org/resize/
    static IEnumerable<string> GetImageMagickArgs(string src, string dst, ScaleSpec scale)
    {
        List<string> args = [
            src
        ];

        if (scale.Width != int.MaxValue)
        {
            // interesting, if we include this above, magick consumes a lot of mem and crashes the process
            // when trying to process the 'full' size.  in this case, we don't try to use a giant size, but
            // looks like magick doesn't like 2 colorspace cmds side by side...
            args.AddRange([
                "-colorspace", "RGB"
            ]);

            if (scale.IsCropToFill)
            {
                args.AddRange([
                    "-resize", $"{scale.Width}x{scale.Height}^",
                    "-gravity", "center",
                    "-crop", $"{scale.Width}x{scale.Height}+0+0"
                ]);
            }
            else
            {
                // the > at the end means "only scale down, not up"
                args.AddRange([
                    "-resize", $"{scale.Width}x{scale.Height}>"
                ]);
            }
        }

        args.AddRange([
            "-colorspace", "sRGB",
            "-quality", "72",
            "-strip",
            dst
        ]);

        return args;
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
