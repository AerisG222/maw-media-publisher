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
                sourceImages.Add(ToKey(file), new MediaFile(file, MediaType.Image));
                continue;
            }

            if (RecognizedExtensions.IsSourceVideo(file))
            {
                sourceVideos.Add(ToKey(file), new MediaFile(file, MediaType.Video));
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
        var dngFiles = supportFiles.Where(sf => RecognizedExtensions.IsDng(sf)).ToList();
        var pp3Files = supportFiles.Where(sf => RecognizedExtensions.IsPp3(sf)).ToList();

        // first process dngs and when doing so, see if those support files reference other support files
        // note: expected naming format: abc.DNG is for abc.NEF
        foreach (var dng in dngFiles)
        {
            if (sourceImages.TryGetValue(ToKey(dng), out var media))
            {
                media.ProcessingFilepath = dng;
                unusedSupportFiles.Remove(dng);
                AssociatePp3(unusedSupportFiles, dng, media);
            }
            else
            {
                // dngs are also valid images, so if it doesn't lineup w/ another file, lets
                // treat it as its own image
                var dngMedia = new MediaFile(dng, MediaType.Image);

                sourceImages.Add(ToKey(dng), dngMedia);
                unusedSupportFiles.Remove(dng);
                AssociatePp3(unusedSupportFiles, dng, dngMedia);
            }
        }

        // now process remaining pp3s that may be associated with the source image
        foreach (var pp3 in pp3Files)
        {
            if (sourceImages.TryGetValue(ToKey(pp3), out var media))
            {
                if (media.SupportFilepath == null)
                {
                    // a more specific pp3 was not set, so set it - otherwise ignore
                    media.SupportFilepath = pp3;
                    unusedSupportFiles.Remove(pp3);
                }
            }
        }

        return new FindResults(
            sourceImages.Values.Concat(sourceVideos.Values),
            unknownFiles.Concat(unusedSupportFiles)
        );
    }

    static void AssociatePp3(HashSet<string> unusedSupportFiles, string dng, MediaFile media)
    {
        // now see if we have pp3 support file for the processing file
        // currently does not handle <dng-file-name>.dng.<garbage>.pp3
        // so we will take the first in the sorted list as the desired pp3
        // and leave the rest as unknown
        var pp3 = unusedSupportFiles
            .Where(usf => usf.StartsWith(dng))
            .Where(f => RecognizedExtensions.IsPp3(f))
            .OrderBy(f => f.Length)
            .FirstOrDefault();

        if (pp3 != null)
        {
            media.SupportFilepath = pp3;
            unusedSupportFiles.Remove(pp3);
        }
    }

    string ToKey(string file) =>
        Path.Combine(Path.GetDirectoryName(file) ?? "", Path.GetFileNameWithoutExtension(file));
}
