using CliWrap;
using MawMediaPublisher.Models;
using Renci.SshNet;
using Spectre.Console;

namespace MawMediaPublisher.Deploy;

public class ProductionDeployer
{
    public async Task Deploy(Category category)
    {
        using var client = new SshClient(
            category.RemoteServer,
            category.RemoteUsername,
            new PrivateKeyFile(category.SshPrivateKeyFile)
        );

        client.Connect();

        AnsiConsole.MarkupLineInterpolated($"[green] Copying files to {category.RemoteServer}[/]");
        EnsureRemoteAssetYearDirectoryExists(category, client);
        await CopyAssetsToRemote(category);

        AnsiConsole.MarkupLineInterpolated($"[green] Applying SQL updates to {category.RemoteServer}[/]");
        ApplySqlScripts(category, client);

        client.Disconnect();
    }

    static void EnsureRemoteAssetYearDirectoryExists(Category category, SshClient client)
    {
        using var cmd = client.RunCommand($"mkdir -p '{category.RemoteYearPath}'");
    }

    static async Task CopyAssetsToRemote(Category category)
    {
        await Cli
            .Wrap("rsync")
            .WithArguments([
                "-ah",
                "--exclude", "*/src*",
                "--exclude", "*.dng",
                category.LocalMediaPath,
                $"{category.RemoteUsername}@{category.RemoteServer}:{category.RemoteYearPath}"
            ])
            .ExecuteAsync();
    }

    static void ApplySqlScripts(Category category, SshClient client)
    {
        var script = Path.GetFileName(category.ScriptFile);
        var sql = Path.GetFileName(category.SqlFile);

        using var cmd1 = client.RunCommand($"cd {category.RemoteMediaPath} && ./{script} prod");
        using var cmd2 = client.RunCommand($"cd {category.RemoteMediaPath} && rm {script}");
        using var cmd3 = client.RunCommand($"cd {category.RemoteMediaPath} && rm {sql}");
    }
}
