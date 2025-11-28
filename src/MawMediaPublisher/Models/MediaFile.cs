using System.Text.RegularExpressions;
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
    public string Slug
    {
        get
        {
            var origFile = Path.GetFileNameWithoutExtension(OriginalFilepath);

            origFile = Regex.Replace(origFile, @"[^a-zA-Z0-9_\-]", string.Empty);

            return origFile
                .Replace("_", "-")
                .ToLower();
        }
    }

    public MediaFile(string file, MediaType type)
    {
        OriginalFilepath = file;
        MediaType = type;
    }
}
