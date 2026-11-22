using CliWrap;
using MawMediaPublisher.Metadata;
using MawMediaPublisher.Models;

namespace MawMediaPublisher.Scale;

class VideoScaler
{
    ExifExporter _exifExporter = new();
    DurationInspector _durationInspector = new();

    public async Task<ScaledFile> Scale(
        Category category,
        FileInfo src,
        string dstDir,
        ScaleSpec scale,
        float? duration
    )
    {
        var dstFilename = $"{Path.GetFileNameWithoutExtension(src.Name)}{(scale.IsPoster ? ".poster.avif" : ".mp4")}";
        var dst = new FileInfo(Path.Combine(dstDir, dstFilename));
        ExifInfo? exif;

        // see if it exists as we may be trying to reprocess
        if (!dst.Exists)
        {
            await ScaleVideo(src, dst, scale, duration);
        }

        try
        {
            exif = await _exifExporter.Export(dst);
        }
        catch
        {
            // if file existed, it was corrput, so lets try again
            File.Delete(dst.FullName);
            await ScaleVideo(src, dst, scale, duration);
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

    async Task ScaleVideo(FileInfo src, FileInfo dst, ScaleSpec scale, float? duration)
    {
        using var cmd = Cli
            .Wrap("ffmpeg")
            .WithArguments(await GetFfmpegArgs(src.FullName, dst.FullName, scale, duration))
            .ExecuteAsync();

        await cmd;
    }

    // https://trac.ffmpeg.org/wiki/Encode/AV1#SVT-AV1
    // https://www.ffmpeg.org/ffmpeg-all.html#scale-1
    // https://evilmartians.com/chronicles/better-web-video-with-av1-codec
    async Task<IEnumerable<string>> GetFfmpegArgs(string src, string dst, ScaleSpec scale, float? duration)
    {
        List<string> args = [
            "-i", src,
            "-map_metadata", "-1"
        ];

        if (scale.Width != int.MaxValue)
        {
            if (scale.IsCropToFill)
            {
                // scale video to fit area (full height or width, with borders as necessary)
                //"-vf", $"\"scale={scale.Width}:{scale.Height}:force_original_aspect_ratio=decrease,pad={scale.Width}:{scale.Height}:(ow-iw)/2:(oh-ih)/2\"",

                // scale video to fit area (cropped to fit), drop sound, 24fps
                args.AddRange([
                    "-an",
                    "-vf", $"scale=(iw*sar)*max({scale.Width}/(iw*sar)\\,{scale.Height}/ih):ih*max({scale.Width}/(iw*sar)\\,{scale.Height}/ih), crop={scale.Width}:{scale.Height}",
                    "-r", "24"
                ]);
            }
            else
            {
                args.AddRange([
                    "-c:a", "aac",
                    "-b:a", "128k",
                    "-vf", $"scale='min({scale.Width},iw)':'min({scale.Height},ih)':force_original_aspect_ratio=decrease:force_divisible_by=2"
                ]);
            }
        }
        else
        {
            args.AddRange([
                "-c:a", "aac",
                "-b:a", "128k",
            ]);
        }

        if (scale.IsPoster)
        {
            // use the duration already captured for the media file, only probe if it wasn't provided
            duration ??= await _durationInspector.Inspect(new FileInfo(src));

            if (duration > 2)
            {
                args.AddRange([
                    "-ss", "00:00:02",
                ]);
            }

            args.AddRange([
                "-frames:v", "1"
            ]);
        }

        // preset 6 / crf 35 tested against svt-av1 defaults (preset 8 / crf 35) on 1080p source: same size,
        // better vmaf (esp worst frames), ~1.9x encode time.  -g 240 = keyframe every ~8-10s for seeking.
        args.AddRange([
            "-c:v", "libsvtav1",
            "-preset", "6",
            "-crf", "35",
            "-g", "240",
            "-movflags", "+faststart",
            dst
        ]);

        return args;
    }
}
