using System.Text.RegularExpressions;

namespace MawMediaPublisher.Models;

public class SlugHelper
{
    public static string MakeSafeSlug(string value)
    {
        var slug = value
                .Replace(" ", "-")
                .Replace("_", "-")
                .Replace("/", "-")
                .Replace("\\", "-")
                .ToLowerInvariant();

        // remove any unallowed characters first, so removing them can't leave consecutive dashes behind
        // (ex: "tom & jerry" -> "tom-&-jerry" -> "tom--jerry")
        var allowed = Regex.Replace(slug, @"[^a-z0-9\-]", string.Empty);

        // remove consecutive dashes, and any leading / trailing dashes
        return Regex.Replace(allowed, @"-{2,}", "-").Trim('-');
    }
}
