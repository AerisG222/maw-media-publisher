namespace MawMediaPublisher.Models;

public class MediaFile
{
    public string OriginalFilepath { get; private set; }
    public MediaType MediaType { get; private set; }
    public string? ProcessingFilepath { get; set; }
    public string? SupportFilepath { get; set; }

    public MediaFile(string file, MediaType type)
    {
        OriginalFilepath = file;
        MediaType = type;
    }
}
