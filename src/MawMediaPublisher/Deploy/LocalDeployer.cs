using System.IO.Compression;
using CliWrap;
using CliWrap.Buffered;
using MawMediaPublisher.Models;
using MawMediaPublisher.Scale;
using Spectre.Console;

namespace MawMediaPublisher.Deploy;

public class LocalDeployer
{
    public const string PP3_ZIP = "pp3.zip";

    public async Task Deploy(Category category)
    {
        var srcDir = Path.Combine(category.SourceDirectory, ScaleSpec.Src.Code);

        if (!Path.Exists(srcDir))
        {
            Directory.CreateDirectory(srcDir);
        }

        MoveOriginalsToSrcDirectory(category, srcDir);
        ZipPp3s(category, srcDir);

        Directory.CreateDirectory(category.LocalYearPath);

        // the following fails when trying to move across drives, so rather than manually copying
        // all files, lets just use good ole mv
        // Directory.Move(category.SourceDirectory, dstDir.FullName);
        await Cli
            .Wrap("mv")
            .WithArguments([
                category.SourceDirectory,
                category.LocalMediaPath
            ])
            .ExecuteAsync();

        await RunLocalImport(category, category.LocalMediaPath);
    }

    static async Task RunLocalImport(Category category, string dstDir)
    {
        const int maxAttempts = 5;
        var attempt = 1;
        var script = Path.Combine(dstDir, Path.GetFileName(category.ScriptFile));

        while (attempt <= maxAttempts)
        {
            using var cmd = Cli
                .Wrap("bash")
                .WithArguments($"{script} dev")
                .WithWorkingDirectory(dstDir)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            var cmdResult = await cmd;

            if (cmdResult.IsSuccess && string.IsNullOrWhiteSpace(cmdResult.StandardError))
            {
                break;
            }

            AnsiConsole.MarkupLine("[bold red]** Error running db import script: **[/]");

            if (!string.IsNullOrWhiteSpace(cmdResult.StandardError))
            {
                AnsiConsole.MarkupLineInterpolated($"[bold red] StdErr: [/][red]{cmdResult.StandardError}[/]");
            }

            if (!string.IsNullOrWhiteSpace(cmdResult.StandardOutput))
            {
                AnsiConsole.MarkupLineInterpolated($"[bold yellow] StdOut: [/][yellow]{cmdResult.StandardOutput}[/]");
            }

            if(attempt < maxAttempts)
            {
                AnsiConsole.MarkupLine("");
                AnsiConsole.MarkupLine("[bold yellow]** If the dev db was not started, please start it now.  Otherwise, you may also tweak the script. **[/]");
                AnsiConsole.MarkupLine("[blue]** Hit Enter to continue or CTL-C to exit **[/]");
                Console.ReadLine();

                attempt++;
            }
            else
            {
                throw new ApplicationException("Error running import script!");
            }
        }
    }

    static void MoveOriginalsToSrcDirectory(Category category, string srcDir)
    {
        foreach (var media in category.Media)
        {
            File.Move(media.OriginalFilepath, Path.Combine(srcDir, Path.GetFileName(media.OriginalFilepath)));

            if (media.SupportFilepath != null)
            {
                File.Move(media.SupportFilepath, Path.Combine(srcDir, Path.GetFileName(media.SupportFilepath)));
            }
        }
    }

    static void ZipPp3s(Category category, string srcDir)
    {
        var filesToZip = Directory
            .EnumerateFiles(srcDir, "*.pp3")
            .ToList();

        // archive pp3s
        if (filesToZip.Count > 0)
        {
            var zipFile = Path.Combine(srcDir, PP3_ZIP);

            using var zipStream = new FileStream(zipFile, FileMode.Create);
            using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create);

            foreach (var file in filesToZip)
            {
                var entry = Path.GetFileName(file);

                zipArchive.CreateEntryFromFile(file, entry, CompressionLevel.Optimal);
            }
        }

        // if the zipfile is created successfully, delete the individual pp3s
        if (filesToZip.Count > 0)
        {
            foreach (var file in filesToZip)
            {
                File.Delete(file);
            }
        }
    }
}
