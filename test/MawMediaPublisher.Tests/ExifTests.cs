using MawMediaPublisher.Metadata;

namespace MawMediaPublisher.Tests;

public class ExifTests
{
    public static TheoryData<string, int, int> MediaSamples => new()
    {
        { Constants.DNG,  5504, 8256 },
        { Constants.NEF,  5520, 8280 },
        { Constants.JPG,  4080, 3072 },
        { Constants.MP4,  1080, 1920 },
    };

    [Theory]
    [MemberData(nameof(MediaSamples))]
    public async Task ValidMedia_CanReadExif(string filename, int height, int width)
    {
        var file = new FileInfo(filename);

        Assert.True(file.Exists);

        var e = new ExifExporter();
        var exif = await e.Export(file);

        Assert.Equal(height, exif.Height);
        Assert.Equal(width, exif.Width);
    }

    // samples were captured in America/New_York (EDT).  the mp4's quicktime date is stored in UTC (23:44:35) and should
    // be converted to local time so it lines up w/ photo exif dates.  note: depends on the local system time zone.
    public static TheoryData<string, string> CreateDateSamples => new()
    {
        { Constants.JPG, "2025-10-04 14:30:57" },
        { Constants.MP4, "2025-10-03 19:44:35" },
    };

    [Theory]
    [MemberData(nameof(CreateDateSamples))]
    public async Task ValidMedia_HasLocalCreateDate(string filename, string expected)
    {
        var exif = await new ExifExporter().Export(new FileInfo(filename));

        Assert.Equal(DateTime.Parse(expected), exif.CreateDate);
    }
}
