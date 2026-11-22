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

    // when the media was captured - from exif when available (see ResolveCreateDate)
    public DateTime CreateDate { get; set; }
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

    // use the exif create date when present.  some files have no (or an invalid, ex: 0000:00:00) create date -
    // screenshots, pngs, images from messaging apps that strip metadata - so fall back to the file's last modified
    // time, which typically still reflects when it was created and keeps the media in a sensible order.
    internal static DateTime ResolveCreateDate(ExifInfo? exif, FileInfo file)
    {
        var exifDate = exif?.CreateDate ?? DateTime.MinValue;

        return exifDate != DateTime.MinValue
            ? exifDate
            : file.LastWriteTime;
    }
}
