using MawMediaPublisher.Metadata;
using MawMediaPublisher.Scale;

namespace MawMediaPublisher.Models;

public class MediaFile
{
    public Guid Id { get; } = Guid.CreateVersion7();
    public string OriginalFilepath { get; private set; }
    public MediaType MediaType { get; private set; }
    public string? ProcessingFilepath { get; set; }
    public string? SupportFilepath { get; set; }
    public ExifInfo? Exif { get; set; }
    public float? VideoDuration { get; set; }
    public IEnumerable<ScaledFile> ScaledFiles { get; set; } = [];
    // defaults to the filename, but may be changed by the finder if it collides w/ another media in the category
    public string Slug { get; set; }
    public string FilepathToProcess => ProcessingFilepath ?? OriginalFilepath;

    public MediaFile(string file, MediaType type)
    {
        OriginalFilepath = file;
        MediaType = type;
        Slug = SlugHelper.MakeSafeSlug(Path.GetFileNameWithoutExtension(file));
    }
}
