using CliWrap;
using MawMediaPublisher.Metadata;
using MawMediaPublisher.Models;

namespace MawMediaPublisher.Scale;

class VideoScaler
{
    OutputInspector _inspector = new();
    DurationInspector _durationInspector = new();

    public async Task<List<ScaledFile>> Scale(
        Category category,
        FileInfo src,
        string baseDir,
        IEnumerable<ScaleSpec> scales,
        float? duration
    )
    {
        var targets = scales
            .Select(scale => new ScaleTarget(
                scale,
                new FileInfo(Path.Combine(
                    baseDir,
                    scale.Code,
                    $"{Path.GetFileNameWithoutExtension(src.Name)}{(scale.IsPoster ? ".poster.avif" : ".mp4")}"
                ))
            ))
            .ToList();

        foreach (var target in targets)
        {
            Directory.CreateDirectory(target.Dst.DirectoryName!);

            // see if it exists as we may be trying to reprocess
            if (!target.Dst.Exists)
            {
                await ScaleVideo(src, target.Dst, target.Scale, duration);
            }
        }

        var info = await _inspector.InspectWithRetry(targets, t => t.Dst, async invalid =>
        {
            foreach (var target in invalid)
            {
                await ScaleVideo(src, target.Dst, target.Scale, duration);
            }
        });

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

    record ScaleTarget(ScaleSpec Scale, FileInfo Dst);
}
