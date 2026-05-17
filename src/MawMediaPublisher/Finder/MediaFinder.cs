using MawMediaPublisher.Models;

namespace MawMediaPublisher.Finder;

public class MediaFinder
{
    public FindResults FindMedia(string dir)
    {
        if (!Directory.Exists(dir))
        {
            throw new ApplicationException($"Directory does not exist: {dir}");
        }

        var files = Directory
            .EnumerateFiles(dir, "*")
            .OrderBy(f => f);

        return BuildMediaFiles(files);
    }

    internal FindResults BuildMediaFiles(IEnumerable<string> files)
    {
        var sourceImages = new Dictionary<string, MediaFile>();
        var sourceVideos = new Dictionary<string, MediaFile>();
        var supportFiles = new List<string>();
        var unknownFiles = new List<string>();

        foreach (var file in files)
        {
            if (RecognizedExtensions.IsSourceImage(file))
            {
                sourceImages.Add(DropExtensionFromFullname(file), new MediaFile(file, MediaType.Image));
                continue;
            }

            if (RecognizedExtensions.IsSourceVideo(file))
            {
                sourceVideos.Add(DropExtensionFromFullname(file), new MediaFile(file, MediaType.Video));
                continue;
            }

            if (RecognizedExtensions.IsSupportFile(file))
            {
                supportFiles.Add(file);
                continue;
            }

            unknownFiles.Add(file);
        }

        var unusedSupportFiles = supportFiles.ToHashSet();

        AssociateDngToSourceImages(sourceImages, supportFiles, unusedSupportFiles);
        AssociatePp3ToSourceImages(sourceImages, supportFiles, unusedSupportFiles);

        return new FindResults(
            sourceImages.Values.Concat(sourceVideos.Values),
            unknownFiles.Concat(unusedSupportFiles)
        );
    }

    void AssociateDngToSourceImages(
        Dictionary<string, MediaFile> sourceImages,
        List<string> supportFiles,
        HashSet<string> unusedSupportFiles
    ) {
        var dngFiles = supportFiles.Where(sf => RecognizedExtensions.IsDng(sf)).ToList();

        // note: expected naming format: abc.DNG is for abc.NEF
        foreach (var dng in dngFiles)
        {
            if (sourceImages.TryGetValue(DropExtensionFromFullname(dng), out var media))
            {
                media.ProcessingFilepath = dng;
                unusedSupportFiles.Remove(dng);
            }
            else
            {
                // dngs are also valid images, so if it doesn't lineup w/ another file, lets
                // treat it as its own image
                var dngMedia = new MediaFile(dng, MediaType.Image);

                sourceImages.Add(DropExtensionFromFullname(dng), dngMedia);
                unusedSupportFiles.Remove(dng);
            }
        }
    }

    static void AssociatePp3ToSourceImages(
        Dictionary<string, MediaFile> sourceImages,
        List<string> supportFiles,
        HashSet<string> unusedSupportFiles
    ) {
        var pp3Files = supportFiles.Where(sf => RecognizedExtensions.IsPp3(sf)).ToList();

        foreach (var pp3 in pp3Files)
        {
            var imageFileForPp3 = DropExtensionFromFullname(pp3);

            // support files like abc123.NEF.pp3 or abc123.dng.pp3
            if (sourceImages.TryGetValue(DropExtensionFromFullname(imageFileForPp3), out var media))
            {
                if(string.Equals(media.FilepathToProcess, imageFileForPp3, StringComparison.OrdinalIgnoreCase))
                {
                    media.SupportFilepath = pp3;
                    unusedSupportFiles.Remove(pp3);
                }
            }
        }
    }

    static string DropExtensionFromFullname(string file) =>
        Path.Combine(Path.GetDirectoryName(file) ?? "", Path.GetFileNameWithoutExtension(file));
}
