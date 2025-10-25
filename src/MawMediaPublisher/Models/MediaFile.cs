using MawMediaPublisher.Metadata;
using MawMediaPublisher.Scale;

namespace MawMediaPublisher.Models;

public class MediaFile
{
    public string OriginalFilepath { get; private set; }
    public MediaType MediaType { get; private set; }
    public string? ProcessingFilepath { get; set; }
    public string? SupportFilepath { get; set; }
    public ExifInfo? Exif { get; set; }
    public float? VideoDuration { get; set; }
    public IEnumerable<ScaledFile> ScaledFiles { get; set; } = [];

    public MediaFile(string file, MediaType type)
    {
        OriginalFilepath = file;
        MediaType = type;
    }
}
