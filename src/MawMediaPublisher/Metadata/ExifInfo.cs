using System.Globalization;
using System.Text.Json;

namespace MawMediaPublisher.Metadata;

public class ExifInfo
{
    const string TAG_IMAGE_HEIGHT = "ImageHeight";
    const string TAG_IMAGE_WIDTH = "ImageWidth";
    const string TAG_CREATE_DATE = "CreateDate";
    const string TAG_GPS_LATITUDE = "GPSLatitude";
    const string TAG_GPS_LONGITUDE = "GPSLongitude";

    public JsonElement Json { get; init; }
    int _height = int.MinValue;
    int _width = int.MinValue;
    decimal? _latitude;
    decimal? _longitude;
    DateTime _createDate = DateTime.MinValue;

    public ExifInfo(JsonElement json)
    {
        Json = json;
    }

    public int Height
    {
        get
        {
            if (_height != int.MinValue)
            {
                return _height;
            }

            _height = GetIntFromVal(FindFirstPropertyByName(Json, TAG_IMAGE_HEIGHT));

            return _height;
        }
    }

    public int Width
    {
        get
        {
            if (_width != int.MinValue)
            {
                return _width;
            }

            _width = GetIntFromVal(FindFirstPropertyByName(Json, TAG_IMAGE_WIDTH));

            return _width;
        }
    }

    public DateTime CreateDate
    {
        get
        {
            if (_createDate != DateTime.MinValue)
            {
                return _createDate;
            }

            _createDate = GetDateTimeFromVal(FindFirstPropertyByName(Json, TAG_CREATE_DATE));

            return _createDate;
        }
    }

    public decimal? Latitude
    {
        get
        {
            if (_latitude != null)
            {
                return _latitude;
            }

            var composite = CompositeElement;

            if(composite != null)
            {
                _latitude = GetDecimalFromNum(
                    FindFirstPropertyByName(composite.Value, TAG_GPS_LATITUDE)
                );
            }

            return _latitude;
        }
    }

    public decimal? Longitude
    {
        get
        {
            if (_longitude != null)
            {
                return _longitude;
            }

            var composite = CompositeElement;

            if(composite != null)
            {
                _longitude = GetDecimalFromNum(
                    FindFirstPropertyByName(composite.Value, TAG_GPS_LONGITUDE)
                );
            }

            return _longitude;
        }
    }

    public JsonElement? CompositeElement
    {
        get
        {
            return FindFirstPropertyByName(Json, "Composite");
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

    private static int GetIntFromVal(JsonElement? prop)
    {
        if (prop == null)
        {
            return int.MinValue;
        }

        var valElem = prop.Value.ValueKind == JsonValueKind.Object && prop.Value.TryGetProperty("val", out var v)
            ? v
            : default;

        if (valElem.ValueKind == JsonValueKind.Number && valElem.TryGetInt32(out var i))
        {
            return i;
        }

        return int.MinValue;
    }

    private static DateTime GetDateTimeFromVal(JsonElement? prop)
    {
        if (prop == null)
        {
            return DateTime.MinValue;
        }

        var valElem = prop.Value.ValueKind == JsonValueKind.Object && prop.Value.TryGetProperty("val", out var v)
            ? v
            : default;

        if (
            valElem.ValueKind == JsonValueKind.String &&
            DateTime.TryParseExact(
                valElem.GetString(),
                "yyyy:MM:dd HH:mm:ss",
                DateTimeFormatInfo.CurrentInfo,
                DateTimeStyles.AllowWhiteSpaces,
                out DateTime dt
            )
        )
        {
            return dt;
        }

        return DateTime.MinValue;
    }

    private static decimal? GetDecimalFromNum(JsonElement? prop)
    {
        if (prop == null)
        {
            return null;
        }

        var valElem = prop.Value.ValueKind == JsonValueKind.Object && prop.Value.TryGetProperty("num", out var v)
            ? v
            : default;

        if (valElem.ValueKind == JsonValueKind.Number && valElem.TryGetDecimal(out var d))
        {
            return d;
        }

        return null;
    }
}
