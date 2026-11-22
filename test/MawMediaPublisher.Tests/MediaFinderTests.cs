using MawMediaPublisher.Finder;
using MawMediaPublisher.Models;

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
            "/a/b/1.jpg.pp3"
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
            "/a/c/1.jpg.pp3"
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

        Assert.Equal(MediaType.Image, media.MediaType);
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

        var media = results.Media.First();

        Assert.Equal(nef, media.OriginalFilepath);
        Assert.Equal(dng, media.ProcessingFilepath);
        Assert.Equal(pp3, media.SupportFilepath);

        Assert.Single(results.Unknown);
        Assert.Equal(nefpp3, results.Unknown.First());
    }

    [Fact]
    public void MediaFinder_ImageAndDngAndSourceSupportFile_AreLinked()
    {
        var mf = new MediaFinder();
        var nef = "/a/b/DSC_123.NEF";
        var nefpp3 = "/a/b/DSC_123.NEF.pp3";
        var dng = "/a/b/DSC_123.dng";

        string[] files = [
            nef,
            nefpp3,
            dng
        ];

        var results = mf.BuildMediaFiles(files);

        Assert.Single(results.Media);

        var media = results.Media.First();

        Assert.Equal(nef, media.OriginalFilepath);
        Assert.Equal(dng, media.ProcessingFilepath);
        Assert.Null(media.SupportFilepath);

        Assert.Single(results.Unknown);
        Assert.Equal(nefpp3, results.Unknown.First());
    }

    [Fact]
    public void MediaFinder_ImageAndVideoWithSameName_GetUniqueSlugs()
    {
        var mf = new MediaFinder();
        string[] files = [
            "/a/b/IMG_1234.HEIC",
            "/a/b/IMG_1234.MOV",
            "/a/b/IMG_5678.HEIC"
        ];

        var results = mf.BuildMediaFiles(files);
        var slugs = results.Media.ToDictionary(m => Path.GetFileName(m.OriginalFilepath), m => m.Slug);

        Assert.Equal("img-1234-heic", slugs["IMG_1234.HEIC"]);
        Assert.Equal("img-1234-mov", slugs["IMG_1234.MOV"]);

        // media that do not collide keep their normal slug
        Assert.Equal("img-5678", slugs["IMG_5678.HEIC"]);
        Assert.Empty(results.FileClashes);
    }

    [Fact]
    public void MediaFinder_NamesThatSlugTheSame_GetUniqueSlugs()
    {
        var mf = new MediaFinder();
        string[] files = [
            "/a/b/IMG_1234.jpg",
            "/a/b/img-1234.jpg",
            // already uses the slug the first suffix would produce
            "/a/b/img-1234-jpg.png"
        ];

        var results = mf.BuildMediaFiles(files);
        var slugs = results.Media.Select(m => m.Slug).ToList();

        Assert.Equal(3, slugs.Count);
        Assert.Equal(3, slugs.Distinct().Count());
        Assert.Contains("img-1234-jpg", slugs);
        Assert.Contains("img-1234-jpg-2", slugs);
        Assert.Contains("img-1234-jpg-3", slugs);
        Assert.Empty(results.FileClashes);
    }

    [Theory]
    [InlineData("/a/b/1.jpg", "/a/b/1.png")]
    [InlineData("/a/b/DSC_1.NEF", "/a/b/DSC_1.JPG")]
    [InlineData("/a/b/1.mp4", "/a/b/1.mov")]
    public void MediaFinder_SameTypeWithSameName_AreReportedAsFileClashes(string first, string second)
    {
        var mf = new MediaFinder();

        var results = mf.BuildMediaFiles([first, second]);

        Assert.Single(results.FileClashes);
        Assert.Equal([first, second], results.FileClashes[0]);
    }
}
