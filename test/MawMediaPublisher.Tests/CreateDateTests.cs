using System.Text.Json;
using MawMediaPublisher.Metadata;
using MawMediaPublisher.Models;

namespace MawMediaPublisher.Tests;

public class CreateDateTests
{
    static readonly DateTime FILE_MODIFIED = new(2024, 7, 4, 13, 14, 15);

    [Theory]
    [InlineData("""{ "ExifIFD": { "CreateDate": { "val": "2026:08:14 16:50:36" } } }""", "2026-08-14 16:50:36")]
    // no create date, or an invalid placeholder, falls back to the file's modified time
    [InlineData("""{ "File": { "FileName": { "val": "x.png" } } }""", "2024-07-04 13:14:15")]
    [InlineData("""{ "ExifIFD": { "CreateDate": { "val": "0000:00:00 00:00:00" } } }""", "2024-07-04 13:14:15")]
    public void ResolveCreateDate_FallsBackToFileModifiedTime(string json, string expected)
    {
        var path = Path.Combine(Path.GetTempPath(), $"mmp-createdate-{Guid.NewGuid()}.png");

        try
        {
            File.WriteAllBytes(path, []);
            File.SetLastWriteTime(path, FILE_MODIFIED);

            var exif = new ExifInfo(JsonDocument.Parse(json).RootElement);
            var result = MediaFile.ResolveCreateDate(exif, new FileInfo(path));

            Assert.Equal(DateTime.Parse(expected), result);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
