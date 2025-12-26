using System.Text.RegularExpressions;

namespace MawMediaPublisher.Models;

public class SlugHelper
{
    public static string MakeSafeSlug(string value)
    {
        var slug = value
                .Replace(" ", "-")
                .Replace("_", "-")
                .ToLower();

        // remove any unallowed characters
        return Regex.Replace(slug, @"[^a-zA-Z0-9_\-]", string.Empty);
    }
}
