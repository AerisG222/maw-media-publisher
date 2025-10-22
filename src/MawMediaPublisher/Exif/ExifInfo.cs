using System.Text.Json;

namespace MawMediaPublisher.Exif;

public class ExifInfo
{
    const string TAG_IMAGE_HEIGHT = "ImageHeight";
    const string TAG_IMAGE_WIDTH = "ImageWidth";

    public JsonElement Json { get; init; }
    int _height = -1;
    int _width = -1;

    public ExifInfo(JsonElement json)
    {
        Json = json;
    }

    public int Height
    {
        get
        {
            if (_height != -1)
            {
                return _height;
            }

            _height = GetIntValue(FindFirstPropertyByName(Json, TAG_IMAGE_HEIGHT));

            return _height;
        }
    }

    public int Width
    {
        get
        {
            if (_width != -1)
            {
                return _width;
            }

            _width = GetIntValue(FindFirstPropertyByName(Json, TAG_IMAGE_WIDTH));

            return _width;
        }
    }

    private static JsonElement? FindFirstPropertyByName(JsonElement root, string name)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return prop.Value;
                }

                var found = FindFirstPropertyByName(prop.Value, name);

                if (found.HasValue)
                {
                    return found;
                }
            }
        }

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                var found = FindFirstPropertyByName(item, name);

                if (found.HasValue)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static int GetIntValue(JsonElement? prop)
    {
        if (prop == null)
        {
            return default;
        }

        var valElem = prop.Value.ValueKind == JsonValueKind.Object && prop.Value.TryGetProperty("val", out var v)
            ? v
            : default;

        if (valElem.ValueKind == JsonValueKind.Number && valElem.TryGetInt32(out var i))
        {
            return i;
        }

        return default;
    }
}
