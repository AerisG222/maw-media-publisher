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
        var clashes = new Dictionary<string, List<string>>();

        foreach (var file in files)
        {
            if (RecognizedExtensions.IsSourceImage(file))
            {
                AddMedia(sourceImages, clashes, file, MediaType.Image);
                continue;
            }

            if (RecognizedExtensions.IsSourceVideo(file))
            {
                AddMedia(sourceVideos, clashes, file, MediaType.Video);
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

        var media = sourceImages.Values.Concat(sourceVideos.Values).ToList();

        AssignUniqueSlugs(media);

        return new FindResults(
            media,
            unknownFiles.Concat(unusedSupportFiles),
            clashes.Values.Select(c => (IReadOnlyList<string>)c).ToList()
        );
    }

    // two images (or two videos) w/ the same name would write the same output files (ex: a.jpg + a.png both produce
    // a.avif), so track these as clashes rather than failing w/ a duplicate key
    static void AddMedia(
        Dictionary<string, MediaFile> media,
        Dictionary<string, List<string>> clashes,
        string file,
        MediaType type
    )
    {
        var key = DropExtensionFromFullname(file);

        if (media.TryAdd(key, new MediaFile(file, type)))
        {
            return;
        }

        if (!clashes.TryGetValue(key, out var files))
        {
            files = [media[key].OriginalFilepath];
            clashes.Add(key, files);
        }

        files.Add(file);
    }

    // slugs must be unique within a category (db constraint).  media whose slugs collide (ex: IMG_1234.HEIC +
    // IMG_1234.MOV, or IMG_1234 + img-1234) get their extension appended, and a number if that still collides.
    // media that do not collide keep their normal slug.
    static void AssignUniqueSlugs(List<MediaFile> media)
    {
        var collisions = media
            .GroupBy(m => m.Slug)
            .Where(g => g.Count() > 1)
            .ToList();

        if (collisions.Count == 0)
        {
            return;
        }

        var taken = media.Select(m => m.Slug).ToHashSet();

        foreach (var group in collisions)
        {
            foreach (var file in group.OrderBy(m => m.OriginalFilepath, StringComparer.Ordinal))
            {
                var ext = SlugHelper.MakeSafeSlug(Path.GetExtension(file.OriginalFilepath).TrimStart('.'));
                var slug = $"{group.Key}-{ext}";
                var candidate = slug;
                var n = 2;

                while (!taken.Add(candidate))
                {
                    candidate = $"{slug}-{n++}";
                }

                file.Slug = candidate;
            }
        }
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
