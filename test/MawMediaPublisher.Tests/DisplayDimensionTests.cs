using System.Text.Json;
using MawMediaPublisher.Metadata;
using MawMediaPublisher.Models;
using MawMediaPublisher.Scale;

namespace MawMediaPublisher.Tests;

public class DisplayDimensionTests
{
    public static TheoryData<MediaType, int?, int, int> Samples => new()
    {
        // stored as 1920x1080 by exif, displayed after rotation
        { MediaType.Video, null, 1920, 1080 },
        { MediaType.Video, 0,    1920, 1080 },
        { MediaType.Video, 90,   1080, 1920 },
        { MediaType.Video, 180,  1920, 1080 },
        { MediaType.Video, 270,  1080, 1920 },
        { MediaType.Video, -90,  1080, 1920 },
        // rotation only applies to videos
        { MediaType.Image, 90,   1920, 1080 },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void DisplayDimensions_AccountForVideoRotation(MediaType type, int? rotation, int expectedWidth, int expectedHeight)
    {
        var composite = rotation == null
            ? ""
            : $$""", "Composite": { "Rotation": { "desc": "Rotation", "val": {{rotation}} } }""";

        var json = JsonDocument.Parse(
            $$"""
            {
                "QuickTime": {
                    "ImageWidth": { "desc": "Image Width", "val": 1920 },
                    "ImageHeight": { "desc": "Image Height", "val": 1080 }
                }{{composite}}
            }
            """
        );

        var file = new MediaFile("/tmp/test.mp4", type)
        {
            Exif = new ExifInfo(json.RootElement)
        };

        var (width, height) = MediaScaler.GetDisplayDimensions(file);

        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
    }
}
