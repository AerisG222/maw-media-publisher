using MawMediaPublisher.Models;

namespace MawMediaPublisher.Scale;

class MediaScaler
{
    readonly PhotoScaler _photoScaler = new();
    readonly VideoScaler _videoScaler = new();

    public async Task<IEnumerable<ScaledFile>> ScaleMedia(MediaFile file)
    {
        var results = new List<ScaledFile>();
        var srcFile = new FileInfo(file.ProcessingFilepath ?? file.OriginalFilepath);
        var origDir = srcFile.Directory!;
        var scales = GetScalesForDimensions(file.Exif!.Width, file.Exif.Height, file.MediaType == MediaType.Video);

        foreach (var scale in scales)
        {
            var scaleDir = Path.Combine(origDir.FullName, scale.Code);

            CreateDir(scaleDir);

            switch (file.MediaType)
            {
                case MediaType.Image:
                    results.Add(await _photoScaler.Scale(srcFile, scaleDir, scale));
                    break;
                case MediaType.Video:
                    results.Add(await _videoScaler.Scale(srcFile, scaleDir, scale));
                    break;
            }
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

    static IEnumerable<ScaleSpec> GetScalesForDimensions(int width, int height, bool includePosters)
    {
        var hasHitMax = false;

        foreach (var scale in ScaleSpec.AllScales)
        {
            if (scale.IsPoster && !includePosters)
            {
                continue;
            }

            // keep 'full' size which will be used for downloads
            if (scale.Width == int.MaxValue)
            {
                yield return scale;
            }

            // if either dimension is greater than the scale bounds, scale it
            if (width > scale.Width || height > scale.Height)
            {
                yield return scale;
            }
            else if (!hasHitMax)
            {
                hasHitMax = true;

                // if we are here, the item fits in the scale bounds, return this last scale so that we can keep the
                // highest res that fits
                yield return scale;
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
