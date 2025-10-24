using MawMediaPublisher.Finder;

namespace MawMediaPublisher.Tests;

public class MediaFinderTets
{
    [Fact]
    public void MediaFinder_EmptyDirectory_ReturnsNoMedia()
    {
        var mf = new MediaFinder();
        var files = new string[0];

        var results = mf.BuildMediaFiles(files);

        Assert.Empty(results.Media);
        Assert.Empty(results.Unknown);
    }

    [Fact]
    public void MediaFinder_DistinctMediaAndUnknown_ReturnsCorrectResults()
    {
        var mf = new MediaFinder();
        string[] files = [
            "/a/b/1.mp4",
            "/a/b/2.jpg",
            "/a/b/3.xyz"
        ];

        var results = mf.BuildMediaFiles(files);

        Assert.Equal(2, results.Media.Count());
        Assert.Single(results.Unknown);
    }

    [Fact]
    public void MediaFinder_ImageAndVideoWithSameName_AreTreatedAsDistinct()
    {
        var mf = new MediaFinder();
        string[] files = [
            "/a/b/1.mp4",
            "/a/b/1.jpg"
        ];

        var results = mf.BuildMediaFiles(files);

        Assert.Equal(2, results.Media.Count());
        Assert.Empty(results.Unknown);
    }

    [Fact]
    public void MediaFinder_ImageAndSupportFile_AreLinked()
    {
        var mf = new MediaFinder();
        string[] files = [
            "/a/b/1.jpg",
            "/a/b/1.pp3"
        ];

        var results = mf.BuildMediaFiles(files);

        Assert.Single(results.Media);
        Assert.NotNull(results.Media.First().SupportFilepath);
    }

    [Fact]
    public void MediaFinder_ImageAndSupportFileInDifferentDirectories_AreNotLinked()
    {
        var mf = new MediaFinder();
        string[] files = [
            "/a/b/1.jpg",
            "/a/c/1.pp3"
        ];

        var results = mf.BuildMediaFiles(files);

        Assert.Single(results.Media);
        Assert.Single(results.Unknown);
    }

    [Fact]
    public void MediaFinder_ImageAndDngAndSupportFile_AreLinked()
    {
        var mf = new MediaFinder();
        var nef = "/a/b/DSC_123.NEF";
        var dng = "/a/b/DSC_123.dng";
        var pp3 = "/a/b/DSC_123.dng.pp3";

        string[] files = [
            nef,
            dng,
            pp3
        ];

        var results = mf.BuildMediaFiles(files);

        Assert.Single(results.Media);

        var media = results.Media.First();

        Assert.Equal(Models.MediaType.Image, media.MediaType);
        Assert.Equal(nef, media.OriginalFilepath);
        Assert.Equal(dng, media.ProcessingFilepath);
        Assert.Equal(pp3, media.SupportFilepath);
        Assert.Empty(results.Unknown);
    }

    [Fact]
    public void MediaFinder_ImageAndDngAndTwoSupportFiles_AreLinked()
    {
        var mf = new MediaFinder();
        var nef = "/a/b/DSC_123.NEF";
        var nefpp3 = "/a/b/DSC_123.NEF.pp3";
        var dng = "/a/b/DSC_123.dng";
        var pp3 = "/a/b/DSC_123.dng.pp3";

        string[] files = [
            nef,
            nefpp3,
            dng,
            pp3
        ];

        var results = mf.BuildMediaFiles(files);

        Assert.Single(results.Media);
        Assert.Single(results.Unknown);
        Assert.Equal(nefpp3, results.Unknown.First());
    }
}
