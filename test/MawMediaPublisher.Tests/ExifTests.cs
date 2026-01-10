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
}
