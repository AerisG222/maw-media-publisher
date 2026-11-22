using MawMediaPublisher.Models;

namespace MawMediaPublisher.Finder;

public class FindResults
{
    public IEnumerable<MediaFile> Media { get; private set; }
    public IEnumerable<string> Unknown { get; private set; }

    // groups of source files that would write to the same output files (ex: a.jpg + a.png, or a.NEF + a.JPG).
    // these must be renamed before processing.
    public IReadOnlyList<IReadOnlyList<string>> FileClashes { get; private set; }

    public FindResults(
        IEnumerable<MediaFile> media,
        IEnumerable<string> unknown,
        IReadOnlyList<IReadOnlyList<string>>? fileClashes = null
    )
    {
        Media = media;
        Unknown = unknown;
        FileClashes = fileClashes ?? [];
    }
}
