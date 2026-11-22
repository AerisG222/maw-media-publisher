using MawMediaPublisher.Models;

namespace MawMediaPublisher.Scale;

class MediaScaler
{
    // videos up to this size are decoded once and all scales are encoded at the same time.  larger videos (ex: 4k)
    // use too much memory when all encoders run at once, so their scales are encoded one at a time
    const long MAX_SINGLE_DECODE_PIXELS = 1920 * 1080;

    readonly PhotoScaler _photoScaler = new();
    readonly VideoScaler _videoScaler = new();

    public async Task<IEnumerable<ScaledFile>> ScaleMedia(Category category, MediaFile file)
    {
        var results = new List<ScaledFile>();
        var srcFile = new FileInfo(file.FilepathToProcess);
        var origDir = srcFile.Directory!;
        var (width, height) = GetDisplayDimensions(file);
        var scales = GetScalesForDimensions(width, height, file.MediaType == MediaType.Video);

        var origFile = new FileInfo(file.OriginalFilepath);

        results.Add(new ScaledFile(
            Guid.CreateVersion7(),
            ScaleSpec.Src,
            category.BuildMediaFilePath(ScaleSpec.Src, origFile.Name),
            width,
            height,
            origFile.Length
        ));

        // the max-fit scale (largest scale the media already fits within) has the same dimensions as 'full', so
        // rather than encoding it again, defer it and copy the 'full' output once that has been produced
        var fullResults = new Dictionary<bool, ScaledFile>();
        var deferred = new List<(int Index, ScaleSpec Scale)>();

        var toScale = new List<ScaleSpec>();

        foreach (var (scale, isMaxFit) in scales)
        {
            if (isMaxFit)
            {
                deferred.Add((results.Count + toScale.Count + deferred.Count, scale));
                continue;
            }

            toScale.Add(scale);
        }

        var scaled = file.MediaType switch
        {
            // photos are scaled to all sizes in a single pass so the source is only decoded once
            MediaType.Image => await _photoScaler.Scale(category, srcFile, origDir.FullName, toScale),
            MediaType.Video => await _videoScaler.Scale(category, srcFile, origDir.FullName, toScale, file.VideoDuration, (long)width * height <= MAX_SINGLE_DECODE_PIXELS),
            _ => throw new InvalidOperationException($"Unexpected media type: {file.MediaType}")
        };

        results.AddRange(scaled);

        foreach (var result in scaled.Where(r => r.Scale.Width == int.MaxValue))
        {
            fullResults[result.Scale.IsPoster] = result;
        }

        // insert in original scale order so the results look the same as before
        foreach (var (index, scale) in deferred)
        {
            var full = fullResults[scale.IsPoster];
            var filename = Path.GetFileName(full.Path);
            var fullFile = Path.Combine(origDir.FullName, full.Scale.Code, filename);
            var scaleDir = Path.Combine(origDir.FullName, scale.Code);
            var dst = Path.Combine(scaleDir, filename);

            CreateDir(scaleDir);
            File.Copy(fullFile, dst, true);

            results.Insert(index, new ScaledFile(
                Guid.CreateVersion7(),
                scale,
                category.BuildMediaFilePath(scale, filename),
                full.Width,
                full.Height,
                full.Bytes
            ));
        }

        // cleanup any intermediary tif files when processing raw files
        if (file.MediaType == MediaType.Image && RecognizedExtensions.IsRaw(srcFile.Name))
        {
            var tif = $"{srcFile.FullName}.tif";

            if (File.Exists(tif))
            {
                File.Delete(tif);
            }
        }

        return results;
    }

    // exif reports the stored (unrotated) dimensions, but ffmpeg applies the rotation when encoding, so use the
    // displayed dimensions when choosing scales.  otherwise portrait phone videos (stored as 1920x1080 w/ a 90
    // rotation) look like they fit full-hd and get a full size copy, and never get a qhd scale.
    internal static (int Width, int Height) GetDisplayDimensions(MediaFile file)
    {
        var exif = file.Exif!;
        var rotation = ((exif.Rotation % 360) + 360) % 360;

        return file.MediaType == MediaType.Video && (rotation == 90 || rotation == 270)
            ? (exif.Height, exif.Width)
            : (exif.Width, exif.Height);
    }

    static IEnumerable<(ScaleSpec Scale, bool IsMaxFit)> GetScalesForDimensions(int width, int height, bool includePosters)
    {
        string? maxScaleCode = null;

        foreach (var scale in ScaleSpec.AllScales)
        {
            if (scale.IsPoster && !includePosters)
            {
                continue;
            }

            // keep 'full' size which will be used for downloads
            if (scale.Width == int.MaxValue)
            {
                yield return (scale, false);
                continue;
            }

            // if either dimension is greater than the scale bounds, scale it
            if (width > scale.Width || height > scale.Height)
            {
                yield return (scale, false);
            }
            else if (maxScaleCode == null || string.Equals(scale.Code, maxScaleCode))
            {
                maxScaleCode = scale.Code;

                // if we are here, the item fits in the scale bounds, return this last scale so that we can keep the
                // highest res that fits.  it will match the 'full' dimensions, so it gets copied from 'full'
                yield return (scale, true);
            }
        }
    }

    static void CreateDir(string dir)
    {
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }
}
