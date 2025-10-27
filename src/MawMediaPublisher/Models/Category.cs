using MawMediaPublisher.Scale;

namespace MawMediaPublisher.Models;

public class Category
{
    public Guid Id { get; } = Guid.CreateVersion7();
    public string Name { get; private set; }
    public string SourceDirectory { get; private set; }
    public DateTime EffectiveDate { get; private set; }
    public string[] Roles { get; private set; }
    public IEnumerable<MediaFile> Media { get; set; } = [];
    public string BaseWebPath => $"/assets/{EffectiveDate.Year}/{BaseDirectoryName}";
    public string BaseDirectoryName => new DirectoryInfo(SourceDirectory).Name;
    public string SqlFile => Path.Combine(SourceDirectory, "category.sql");
    public string ScriptFile => Path.Combine(SourceDirectory, "import.sh");

    public Category(
        string name,
        string sourceDirectory,
        DateTime effectiveDate,
        string roles
    )
    {
        Name = name;
        SourceDirectory = sourceDirectory;
        EffectiveDate = effectiveDate;
        Roles = roles.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public string BuildMediaFilePath(ScaleSpec spec, string filename) =>
        Path.Combine(BaseWebPath, spec.Code, Path.GetFileName(filename));
}
