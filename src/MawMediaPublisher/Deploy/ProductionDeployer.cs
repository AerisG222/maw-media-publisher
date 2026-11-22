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
        RunRemote(client, $"mkdir -p {ShellQuote(category.RemoteYearPath)}", "Create remote year directory");
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
        var dir = ShellQuote(category.RemoteMediaPath);
        var script = ShellQuote(Path.GetFileName(category.ScriptFile));
        var sql = ShellQuote(Path.GetFileName(category.SqlFile));

        // the import script runs psql w/ ON_ERROR_STOP, so a failed import returns a non-zero exit status.  if it
        // fails, leave the script and sql on the server so the import can be fixed / re-run manually.
        RunRemote(client, $"cd {dir} && ./{script} prod", "Production SQL import", category.RemoteMediaPath);
        RunRemote(client, $"cd {dir} && rm {script} {sql}", "Remove import script and sql");
    }

    static void RunRemote(SshClient client, string command, string description, string? retryDir = null)
    {
        using var cmd = client.RunCommand(command);

        if (!string.IsNullOrWhiteSpace(cmd.Error))
        {
            AnsiConsole.MarkupLineInterpolated($"[bold yellow] {description} - StdErr: [/][yellow]{cmd.Error}[/]");
        }

        if (cmd.ExitStatus == 0)
        {
            return;
        }

        AnsiConsole.MarkupLineInterpolated($"[bold red]** {description} failed (exit status {cmd.ExitStatus}) **[/]");

        if (!string.IsNullOrWhiteSpace(cmd.Result))
        {
            AnsiConsole.MarkupLineInterpolated($"[bold yellow] StdOut: [/][yellow]{cmd.Result}[/]");
        }

        if (retryDir != null)
        {
            AnsiConsole.MarkupLineInterpolated($"[bold yellow]** The import script and sql were left in {retryDir} so the import can be re-run manually. **[/]");
        }

        throw new ApplicationException($"{description} failed on the remote server (exit status {cmd.ExitStatus})");
    }

    // wrap in single quotes for the remote shell, escaping any single quotes in the value
    internal static string ShellQuote(string value) => $"'{value.Replace("'", "'\\''")}'";
}
