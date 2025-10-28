using MawMediaPublisher.Finder;
using MawMediaPublisher.Metadata;
using MawMediaPublisher.Models;
using MawMediaPublisher.Scale;

namespace MawMediaPublisher.Tests;

public class ScaleTests
{
    [Fact(Skip = "skip by default as this takes a while and requires cleanup - intended for manual execution")]
    public async Task TestFiles_CanBe_Scaled()
    {
        var mf = new MediaFinder();
        var ee = new ExifExporter();
        var ms = new MediaScaler();
        var fi = new FileInfo(Constants.NEF);
        var category = new Category(
            "category-name",
            "/my/src/dir/awesome-photos",
            DateTime.Now,
            "admin friend",
            "/data/local",
            "/data/remote"
        );

        var results = mf.FindMedia(fi.DirectoryName!);

        foreach (var media in results.Media)
        {
            media.Exif = await ee.Export(new FileInfo(media.OriginalFilepath));
            var scaleResult = await ms.ScaleMedia(category, media);

            Assert.NotNull(scaleResult);
        }
    }
}
