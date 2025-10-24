using MawMediaPublisher.Models;

namespace MawMediaPublisher.Finder;

public class FindResults
{
    public IEnumerable<MediaFile> Media { get; private set; }
    public IEnumerable<string> Unknown { get; private set; }

    public FindResults(IEnumerable<MediaFile> media, IEnumerable<string> unknown)
    {
        Media = media;
        Unknown = unknown;
    }
}
