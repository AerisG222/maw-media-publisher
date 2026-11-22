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
        float? duration,
        bool singleDecode
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
        }

        // see if they exist as we may be trying to reprocess
        await Encode(src, targets.Where(t => !t.Dst.Exists).ToList(), duration, singleDecode);

        var info = await _inspector.InspectWithRetry(targets, t => t.Dst, invalid => Encode(src, invalid, duration, singleDecode));

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

    async Task Encode(FileInfo src, IReadOnlyList<ScaleTarget> targets, float? duration, bool singleDecode)
    {
        var videos = targets.Where(t => !t.Scale.IsPoster).ToList();

        if (singleDecode && videos.Count > 1)
        {
            await ScaleVideos(src, videos);
        }
        else
        {
            foreach (var target in videos)
            {
                await ScaleVideo(src, target.Dst, target.Scale);
            }
        }

        var posters = targets.Where(t => t.Scale.IsPoster).ToList();

        if (posters.Count > 0)
        {
            await ScalePosters(src, posters, duration);
        }
    }

    static async Task ScaleVideo(FileInfo src, FileInfo dst, ScaleSpec scale)
    {
        await Cli
            .Wrap("ffmpeg")
            .WithArguments(GetFfmpegArgs(src.FullName, dst.FullName, scale))
            .ExecuteWithErrorsAsync();
    }

    // decode the source once and encode all scales from a single ffmpeg process (w/ the split filter).  tested on a
    // 1080p source: ~31% faster for a single video (27.5s -> 18.8s) and ~18% faster for a batch of 4, but all
    // encoders run at once so it uses more memory (1080p: ~4.5GB vs ~3.3GB, 4k: ~17.5GB vs ~10GB), which is why
    // it is only used for sources up to 1080p.
    static async Task ScaleVideos(FileInfo src, IReadOnlyList<ScaleTarget> targets)
    {
        await Cli
            .Wrap("ffmpeg")
            .WithArguments(GetMultiOutputFfmpegArgs(src.FullName, targets))
            .ExecuteWithErrorsAsync();
    }

    // posters are produced by extracting a single (lossless) frame and then scaling that like a photo, rather than
    // running ffmpeg for each poster size.  this is faster and lets us use the same avif encoder as photos, which
    // gives noticeably better quality for the same size than encoding the poster w/ svt-av1.
    async Task ScalePosters(FileInfo src, IReadOnlyList<ScaleTarget> posters, float? duration)
    {
        var frame = $"{src.FullName}.poster.png";

        // use the duration already captured for the media file, only probe if it wasn't provided
        duration ??= await _durationInspector.Inspect(src);

        try
        {
            await Cli
                .Wrap("ffmpeg")
                .WithArguments(GetFrameExtractArgs(src.FullName, frame, duration > 2))
                .ExecuteWithErrorsAsync();

            await AvifWriter.Write(frame, posters, AvifWriter.POSTER_QUALITY);
        }
        finally
        {
            File.Delete(frame);
        }
    }

    static IEnumerable<string> GetFrameExtractArgs(string src, string dst, bool skipIntro)
    {
        List<string> args = [
            "-hide_banner",
            "-loglevel", "error",
            "-y"
        ];

        // seek to 2s (if the video is long enough) to avoid black / fade-in frames at the start
        if (skipIntro)
        {
            args.AddRange(["-ss", "00:00:02"]);
        }

        args.AddRange([
            "-i", src,
            "-frames:v", "1",
            // convert to square pixels (if needed) so the poster has the correct aspect ratio once scaled as a photo
            "-vf", "scale='trunc(iw*sar)':ih,setsar=1",
            // 16bit rgb png to avoid any loss before scaling
            "-pix_fmt", "rgb48be",
            dst
        ]);

        return args;
    }

    // preset 6 / crf 35 tested against svt-av1 defaults (preset 8 / crf 35) on 1080p source: same size,
    // better vmaf (esp worst frames), ~1.9x encode time.  -g 240 = keyframe every ~8-10s for seeking.
    static readonly string[] VIDEO_ENCODE_ARGS = [
        "-c:v", "libsvtav1",
        "-preset", "6",
        "-crf", "35",
        "-g", "240",
        "-movflags", "+faststart"
    ];

    static readonly string[] AUDIO_ENCODE_ARGS = [
        "-c:a", "aac",
        "-b:a", "128k"
    ];

    // https://trac.ffmpeg.org/wiki/Encode/AV1#SVT-AV1
    // https://www.ffmpeg.org/ffmpeg-all.html#scale-1
    // https://evilmartians.com/chronicles/better-web-video-with-av1-codec
    //
    // returns the video filter (null when the video is not scaled) and output options for the scale
    static (string? Filter, string[] OutputArgs) GetScaleSettings(ScaleSpec scale)
    {
        if (scale.Width == int.MaxValue)
        {
            return (null, AUDIO_ENCODE_ARGS);
        }

        if (scale.IsCropToFill)
        {
            // scale video to fit area (full height or width, with borders as necessary)
            //"-vf", $"\"scale={scale.Width}:{scale.Height}:force_original_aspect_ratio=decrease,pad={scale.Width}:{scale.Height}:(ow-iw)/2:(oh-ih)/2\"",

            // scale video to fit area (cropped to fit), drop sound, 24fps
            return (
                $"scale=(iw*sar)*max({scale.Width}/(iw*sar)\\,{scale.Height}/ih):ih*max({scale.Width}/(iw*sar)\\,{scale.Height}/ih), crop={scale.Width}:{scale.Height}",
                ["-an", "-r", "24"]
            );
        }

        return (
            $"scale='min({scale.Width},iw)':'min({scale.Height},ih)':force_original_aspect_ratio=decrease:force_divisible_by=2",
            AUDIO_ENCODE_ARGS
        );
    }

    static IEnumerable<string> GetFfmpegArgs(string src, string dst, ScaleSpec scale)
    {
        var (filter, outputArgs) = GetScaleSettings(scale);

        List<string> args = [
            "-hide_banner",
            "-loglevel", "error",
            "-i", src,
            "-map_metadata", "-1",
            .. outputArgs
        ];

        if (filter != null)
        {
            args.AddRange(["-vf", filter]);
        }

        args.AddRange([
            .. VIDEO_ENCODE_ARGS,
            dst
        ]);

        return args;
    }

    // same settings as GetFfmpegArgs, but the decoded video is split to one filter chain / output per scale
    static IEnumerable<string> GetMultiOutputFfmpegArgs(string src, IReadOnlyList<ScaleTarget> targets)
    {
        var settings = targets.Select(t => GetScaleSettings(t.Scale)).ToList();
        var graph = new List<string>
        {
            $"[0:v:0]split={targets.Count}{string.Concat(targets.Select((_, i) => $"[v{i}]"))}"
        };

        for (var i = 0; i < targets.Count; i++)
        {
            if (settings[i].Filter != null)
            {
                graph.Add($"[v{i}]{settings[i].Filter}[o{i}]");
            }
        }

        List<string> args = [
            "-hide_banner",
            "-loglevel", "error",
            "-i", src,
            "-filter_complex", string.Join(';', graph)
        ];

        for (var i = 0; i < targets.Count; i++)
        {
            var (filter, outputArgs) = settings[i];

            args.AddRange(["-map", filter == null ? $"[v{i}]" : $"[o{i}]"]);

            // when not dropping audio, include the first audio stream (if there is one)
            if (!outputArgs.Contains("-an"))
            {
                args.AddRange(["-map", "0:a:0?"]);
            }

            args.AddRange([
                "-map_metadata", "-1",
                .. outputArgs,
                .. VIDEO_ENCODE_ARGS,
                targets[i].Dst.FullName
            ]);
        }

        return args;
    }

}
