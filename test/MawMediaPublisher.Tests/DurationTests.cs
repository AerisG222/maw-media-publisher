using MawMediaPublisher.Metadata;

namespace MawMediaPublisher.Tests;

public class DurationTests
{
    [Fact]
    public async Task ValidVideo_CanRead_Duration()
    {
        var file = new FileInfo(Constants.MP4);

        Assert.True(file.Exists);

        var di = new DurationInspector();
        var duration = await di.Inspect(file);

        Assert.Equal(31.792544, duration, 0.00001);
    }
}
