using System.Text.RegularExpressions;
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
    public string LocalAssetRoot { get; private set; }
    public string RemoteAssetRoot { get; private set; }
    public string RemoteServer { get; private set; }
    public string RemoteUsername { get; private set; }
    public string SshPrivateKeyFile { get; private set; }
    public string BaseDirectoryName => new DirectoryInfo(SourceDirectory).Name;
    public string BaseWebUrl => $"/assets/{EffectiveDate.Year}/{BaseDirectoryName}";
    public string LocalYearPath => Path.Combine(LocalAssetRoot, EffectiveDate.Year.ToString());
    public string LocalMediaPath => Path.Combine(LocalYearPath, BaseDirectoryName);
    public string RemoteYearPath => Path.Combine(RemoteAssetRoot, EffectiveDate.Year.ToString());
    public string RemoteMediaPath => Path.Combine(RemoteYearPath, BaseDirectoryName);
    public string SqlFile => Path.Combine(SourceDirectory, "category.sql");
    public string ScriptFile => Path.Combine(SourceDirectory, "import.sh");
    public string Slug
    {
        get
        {
            var slug = Regex.Replace(Name, @"[^a-zA-Z0-9_\-]", string.Empty);

            return slug
                .Replace("_", "-")
                .ToLower();
        }
    }

    public Category(
        string name,
        string sourceDirectory,
        DateTime effectiveDate,
        string roles,
        string localAssetRoot,
        string remoteAssetRoot,
        string remoteServer,
        string remoteUsername,
        string sshPrivateKeyFile
    )
    {
        Name = name;
        SourceDirectory = sourceDirectory;
        EffectiveDate = effectiveDate;
        Roles = roles.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        LocalAssetRoot = localAssetRoot;
        RemoteAssetRoot = remoteAssetRoot;
        RemoteServer = remoteServer;
        RemoteUsername = remoteUsername;
        SshPrivateKeyFile = sshPrivateKeyFile;
    }

    public string BuildMediaFilePath(ScaleSpec spec, string filename) =>
        Path.Combine(BaseWebUrl, spec.Code, Path.GetFileName(filename));
}
