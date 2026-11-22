using MawMediaPublisher.Models;

namespace MawMediaPublisher.Scale;

class MediaScaler
{
    readonly PhotoScaler _photoScaler = new();
    readonly VideoScaler _videoScaler = new();

    public async Task<IEnumerable<ScaledFile>> ScaleMedia(Category category, MediaFile file)
    {
        var results = new List<ScaledFile>();
        var srcFile = new FileInfo(file.FilepathToProcess);
        var origDir = srcFile.Directory!;
        var scales = GetScalesForDimensions(file.Exif!.Width, file.Exif.Height, file.MediaType == MediaType.Video);

        var origFile = new FileInfo(file.OriginalFilepath);

        results.Add(new ScaledFile(
            Guid.CreateVersion7(),
            ScaleSpec.Src,
            category.BuildMediaFilePath(ScaleSpec.Src, origFile.Name),
            file.Exif.Width,
            file.Exif.Height,
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
            MediaType.Video => await ScaleVideo(category, file, srcFile, origDir.FullName, toScale),
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

    async Task<List<ScaledFile>> ScaleVideo(Category category, MediaFile file, FileInfo srcFile, string baseDir, IEnumerable<ScaleSpec> scales)
    {
        var results = new List<ScaledFile>();

        foreach (var scale in scales)
        {
            var scaleDir = Path.Combine(baseDir, scale.Code);

            CreateDir(scaleDir);

            results.Add(await _videoScaler.Scale(category, srcFile, scaleDir, scale, file.VideoDuration));
        }

        return results;
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
