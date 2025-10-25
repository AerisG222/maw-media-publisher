namespace MawMediaPublisher.Models;

public class Category
{
    public string Name { get; private set; }
    public string SourceDirectory { get; private set; }
    public DateTime EffectiveDate { get; private set; }
    public IEnumerable<MediaFile> Media { get; set; } = [];
    public string BaseWebPath => $"/assets/{EffectiveDate.Year}/{BaseDirectoryName}";
    public string BaseDirectoryName => new DirectoryInfo(SourceDirectory).Name;

    public Category(
        string name,
        string sourceDirectory,
        DateTime effectiveDate
    )
    {
        Name = name;
        SourceDirectory = sourceDirectory;
        EffectiveDate = effectiveDate;
    }
}
