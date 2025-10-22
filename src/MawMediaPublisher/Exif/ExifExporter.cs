using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;

namespace MawMediaPublisher.Exif;

class ExifExporter
{
    public async Task<JsonElement> Export(FileInfo file)
    {
        return await ExtractExif(file);
    }

    async Task<JsonElement> ExtractExif(FileInfo file)
    {
        using var cmd = Cli
            .Wrap("exiftool")
            .WithArguments([
                "-json",
                "-quiet",
                "-groupHeadings",
                "-long",
                file.FullName
            ])
            .ExecuteBufferedAsync();

        var cmdResult = await cmd;

        if (string.IsNullOrWhiteSpace(cmdResult.StandardOutput))
        {
            throw new InvalidDataException($"Unable to read json from exiftool for: {file.FullName}");
        }

        var exif = JsonDocument.Parse(cmdResult.StandardOutput);

        if (exif == null)
        {
            throw new ApplicationException($"Unable to parse exif data for: {file.FullName}");
        }

        if (exif.RootElement.ValueKind == JsonValueKind.Array)
        {
            if (exif.RootElement.EnumerateArray().Count() != 1)
            {
                throw new ApplicationException($"Only expected a single result when parsing exif for: {file.FullName}.  Count was: {exif.RootElement.EnumerateArray().Count()}");
            }

            return exif.RootElement.EnumerateArray().First();
        }

        if (exif.RootElement.ValueKind == JsonValueKind.Object)
        {
            return exif.RootElement;
        }

        throw new ApplicationException($"Did not find expected json result when parsing exif for: {file.FullName}");
    }
}
