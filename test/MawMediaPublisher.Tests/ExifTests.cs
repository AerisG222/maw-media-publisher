using System.Text.Json;
using MawMediaPublisher.Exif;

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
        var json = await e.Export(file);

        Assert.Equal(height, GetImageHeight(json));
        Assert.Equal(width, GetImageWidth(json));
    }

    int GetImageHeight(JsonElement json)
    {
        var prop = FindFirstPropertyByName(json, Constants.TAG_IMAGE_HEIGHT);
        if (prop.HasValue)
        {
            var valElem = prop.Value.Value
                .ValueKind == JsonValueKind.Object && prop.Value.Value.TryGetProperty("val", out var v)
                ? v
                : default;

            if (valElem.ValueKind == JsonValueKind.Number && valElem.TryGetInt32(out var i))
                return i;
        }

        throw new KeyNotFoundException($"Could not find integer 'val' for '{Constants.TAG_IMAGE_HEIGHT}' in JSON.");
    }

    int GetImageWidth(JsonElement json)
    {
        var prop = FindFirstPropertyByName(json, Constants.TAG_IMAGE_WIDTH);
        if (prop.HasValue)
        {
            var valElem = prop.Value.Value
                .ValueKind == JsonValueKind.Object && prop.Value.Value.TryGetProperty("val", out var v)
                ? v
                : default;

            if (valElem.ValueKind == JsonValueKind.Number && valElem.TryGetInt32(out var i))
                return i;
        }

        throw new KeyNotFoundException($"Could not find integer 'val' for '{Constants.TAG_IMAGE_WIDTH}' in JSON.");
    }

    // Searches the JsonElement tree for the first property with the given name.
    // Returns a KeyValuePair containing the property name and its JsonElement when found, otherwise null.
    KeyValuePair<string, JsonElement>? FindFirstPropertyByName(JsonElement root, string name)
    {
        // If root is an object, iterate its properties
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                    return new KeyValuePair<string, JsonElement>(prop.Name, prop.Value);

                // Recurse into property value
                var found = FindFirstPropertyByName(prop.Value, name);
                if (found.HasValue)
                    return found;
            }
        }

        // If root is an array, iterate elements
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                var found = FindFirstPropertyByName(item, name);
                if (found.HasValue)
                    return found;
            }
        }

        return null;
    }
}
