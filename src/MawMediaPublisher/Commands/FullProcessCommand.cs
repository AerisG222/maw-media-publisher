using System.ComponentModel;
using System.IO.Compression;
using CliWrap;
using CliWrap.Buffered;
using Spectre.Console;
using Spectre.Console.Cli;
using MawMediaPublisher.Metadata;
using MawMediaPublisher.Finder;
using MawMediaPublisher.Models;
using MawMediaPublisher.Scale;
using MawMediaPublisher.Sql;

namespace MawMediaPublisher.Commands;

internal sealed class FullProcessCommand
    : AsyncCommand<FullProcessCommand.Settings>
{
    const int STATUS_SUCCESS = 0;
    const int STATUS_USER_CANCELLED = 1;
    const int STATUS_DESTINATION_EXISTS = 2;

    static readonly MediaFinder _mediaFinder = new();
    static readonly ExifExporter _exifExporter = new();
    static readonly DurationInspector _durationInspector = new();
    static readonly MediaScaler _mediaScaler = new();
    static readonly SqlWriter _sqlWriter = new();
    static readonly ScriptWriter _scriptWriter = new();
    static readonly Lock _lock = new();

    public sealed class Settings
        : CommandSettings
    {
        [CommandOption("-m|--media-path", true)]
        [Description("Path to to media files for this category.")]
        [DirectoryExists("Please specify an existing directory for the media path.")]
        public string MediaPath { get; init; } = "";

        [CommandOption("-c|--category-name", true)]
        [Description("Name of the category.")]
        public string CategoryName { get; init; } = "";

        [CommandOption("-d|--effective-date", false)]
        [Description("Effective date, in (yyyy-mm-dd) format, to use for this category.  Defaults to now.")]
        public DateTime EffectiveDate { get; init; } = DateTime.Now;

        [CommandOption("-r|--roles", false)]
        [Description("Space delimited list of roles that should have access to the category")]
        [DefaultValue("admin friend")]
        public string Roles { get; init; } = "";

        [CommandOption("-i|--interactive", false)]
        [Description("Interactive mode - illustrate steps and prompt to continue before executing anything.")]
        public bool Interactive { get; init; }

        [CommandOption("--local-asset-root", false)]
        [Description("Local Asset Root - root directory where assets should be stored on local / processing machine.")]
        [DefaultValue("/data/maw-media-assets")]
        public string LocalAssetRoot { get; init; } = "";

        [CommandOption("--remote-asset-root", false)]
        [Description("Remote Asset Root - root directory where assets should be stored on remote / production server.")]
        [DefaultValue("/home/svc_maw_media/maw-media/media-assets")]
        public string RemoteAssetRoot { get; init; } = "";
    }

    public async override Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings
    )
    {
        var category = new Category(
            settings.CategoryName,
            settings.MediaPath,
            settings.EffectiveDate,
            settings.Roles,
            settings.LocalAssetRoot,
            settings.RemoteAssetRoot
        );

        if (Directory.Exists(category.LocalAssetPath))
        {
            AnsiConsole.MarkupLineInterpolated($"[bold red]!!** Asset directory {category.LocalAssetPath} already exists.  Please choose a unique directory name **!![/]");

            return STATUS_DESTINATION_EXISTS;
        }

        if (settings.Interactive && !PrintParametersAndContinue(category))
        {
            return STATUS_USER_CANCELLED;
        }

        var foundFiles = _mediaFinder.FindMedia(category.SourceDirectory);

        if (settings.Interactive && !PrintMediaToProcessAndContinue(foundFiles))
        {
            return STATUS_USER_CANCELLED;
        }

        category.Media = foundFiles.Media;

        await ProcessCategoryMedia(category);

        AnsiConsole.MarkupLine("[yellow]** All files have been processed.  Please review to make sure they came out as expected.[/]");

        if (!ContinuePrompt("Development Deploy (please make sure dev pod is running)?"))
        {
            return STATUS_USER_CANCELLED;
        }

        await LocalDeploy(category);

        if (!ContinuePrompt("Production Deploy?"))
        {
            return STATUS_USER_CANCELLED;
        }

        await ProductionDeploy(category);

        if (!ContinuePrompt("Remote Asset Archive?"))
        {
            return STATUS_USER_CANCELLED;
        }

        await RemoteArchive(category);

        AnsiConsole.MarkupLine("[yellow]** COMPLETED **[/]");

        return STATUS_SUCCESS;
    }

    static async Task ProcessCategoryMedia(Category category)
    {
        await AnsiConsole.Progress()
            .Columns([
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn(),
            ])
            .StartAsync(async ctx =>
            {
                var pctPerFile = (1.0 / category.Media.Count()) * 100;
                var task = ctx.AddTask("[green]Processing Media[/]");

                // from the migration app:
                // when processing highest res image to full hi res destination, we kept crashing
                // as we ran out of mem - for a single run, it looks like 1 image can take upwards of 5gb,
                // so set max parallelism to 12 to try and stay below our sys mem (currently 64gb)
                var opts = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 12
                };

                await Parallel.ForEachAsync(category.Media, opts, async (media, token) =>
                {
                    await ProcessMedia(category, media);

                    lock (_lock)
                    {
                        task.Increment(pctPerFile);
                    }
                });

                await GenerateSql(category);
            });
    }

    static async Task ProcessMedia(Category category, MediaFile file)
    {
        var origFile = new FileInfo(file.OriginalFilepath);

        file.Exif = await _exifExporter.Export(origFile);

        if (file.Exif == null)
        {
            throw new ApplicationException("Exif data is required!");
        }

        if (file.MediaType == MediaType.Video)
        {
            file.VideoDuration = await _durationInspector.Inspect(origFile);
        }

        file.ScaledFiles = await _mediaScaler.ScaleMedia(category, file);
    }

    static async Task GenerateSql(Category category)
    {
        await _sqlWriter.GenerateSql(category);
        await _scriptWriter.WriteRunnerScript(category);
    }

    static async Task LocalDeploy(Category category)
    {
        var srcDir = Path.Combine(category.SourceDirectory, ScaleSpec.Src.Code);

        if (!Path.Exists(srcDir))
        {
            Directory.CreateDirectory(srcDir);
        }

        MoveOriginalsToSrcDirectory(category, srcDir);
        ZipPp3s(category, srcDir);

        var dstDir = new DirectoryInfo(category.LocalAssetPath);
        var parentDir = dstDir.Parent!;

        if (!parentDir.Exists)
        {
            parentDir.Create();
        }

        // the following fails when trying to move across drives, so rather than manually copying
        // all files, lets just use good ole mv
        // Directory.Move(category.SourceDirectory, dstDir.FullName);
        await Cli
            .Wrap("mv")
            .WithArguments([
                category.SourceDirectory,
                dstDir.FullName
            ])
            .ExecuteAsync();

        await RunLocalImport(category, dstDir);
    }

    static async Task RunLocalImport(Category category, DirectoryInfo dstDir)
    {
        var script = Path.Combine(dstDir.FullName, Path.GetFileName(category.ScriptFile));

        using var cmd = Cli
            .Wrap("bash")
            .WithArguments($"{script} dev")
            .WithWorkingDirectory(dstDir.FullName)
            .ExecuteBufferedAsync();

        var cmdResult = await cmd;

        if (!cmdResult.IsSuccess)
        {
            AnsiConsole.MarkupLine("[bold red]** Error running db import script: **[/]");
            AnsiConsole.MarkupLineInterpolated($"[bold red] StdErr: [/][red]{cmdResult.StandardError}[/]");
            AnsiConsole.MarkupLineInterpolated($"[bold yellow] StdOut: [/][yellow]{cmdResult.StandardOutput}[/]");

            throw new ApplicationException("Error running import script!");
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
        var filesToZip = Directory.EnumerateFiles(srcDir, "*.pp3");

        // archive pp3s
        if (filesToZip.Any())
        {
            var zipFile = Path.Combine(srcDir, "pp3.zip");

            using var zipStream = new FileStream(zipFile, FileMode.Create);
            using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create);

            foreach (var file in filesToZip)
            {
                var entry = Path.GetFileName(file);

                zipArchive.CreateEntryFromFile(file, entry, CompressionLevel.Optimal);
            }
        }

        // if the zipfile is created successfuly, delete the individual pp3s
        if (filesToZip.Any())
        {
            foreach (var file in filesToZip)
            {
                File.Delete(file);
            }
        }
    }

    static Task ProductionDeploy(Category category)
    {
        throw new NotImplementedException();
    }

    static Task RemoteArchive(Category category)
    {
        throw new NotImplementedException();
    }

    static bool PrintParametersAndContinue(Category category)
    {
        OutputHeader("Parameters");
        OutputVariable("    Category Name", category.Name);
        OutputVariable("   Effective Date", category.EffectiveDate.ToString("yyyy-MM-dd"));
        OutputVariable("     Media Source", category.SourceDirectory);
        OutputVariable("     Base Web URL", category.BaseWebUrl);
        OutputVariable("            Roles", string.Join(" ", category.Roles));
        OutputVariable(" Local Asset Path", category.LocalAssetPath);
        OutputVariable("Remote Asset Path", category.RemoteAssetPath);

        AnsiConsole.WriteLine();

        return ContinuePrompt();
    }

    static bool PrintMediaToProcessAndContinue(FindResults files)
    {
        var grid = new Grid();

        grid.AddColumn();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow([
            new Text("UNK", new Style(Color.Yellow, decoration: Decoration.Bold)),
            new Text("Type", new Style(Color.Yellow, decoration: Decoration.Bold)),
            new Text("Original", new Style(Color.Yellow, decoration: Decoration.Bold)),
            new Text("Processing", new Style(Color.Yellow, decoration: Decoration.Bold)),
            new Text("Support", new Style(Color.Yellow, decoration: Decoration.Bold)),
        ]);

        foreach (var file in files.Media)
        {
            grid.AddRow([
                new Text("", new Style(Color.Green)),
                new Text(MediaTypeDisplayForGrid(file.MediaType), new Style(Color.Green)),
                new Text(Path.GetFileName(file.OriginalFilepath), new Style(Color.Green)),
                new Text(Path.GetFileName(file.ProcessingFilepath) ?? "", new Style(Color.Green)),
                new Text(Path.GetFileName(file.SupportFilepath) ?? "", new Style(Color.Green)),
            ]);
        }

        foreach (var file in files.Unknown)
        {
            grid.AddRow([
                new Text("!!", new Style(Color.Red)),
                new Text(""),
                new Text(Path.GetFileName(file), new Style(Color.Red)),
                new Text(""),
                new Text(""),
            ]);
        }

        OutputHeader("Files to Process");
        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();

        if (files.Unknown.Any())
        {
            AnsiConsole.MarkupLine("[red]!! Unknown files will not be processed !![/]");
            AnsiConsole.WriteLine();
        }

        return ContinuePrompt();
    }

    static string MediaTypeDisplayForGrid(MediaType mt) => mt switch
    {
        MediaType.Image => "p",
        MediaType.Video => "v",
        _ => "?"
    };

    static bool ContinuePrompt(string? title = null)
    {
        return AnsiConsole.Prompt(new ConfirmationPrompt(title ?? "Continue?"));
    }

    static void OutputVariable(string name, string value)
    {
        AnsiConsole.MarkupLineInterpolated($"[bold yellow]{name}:[/] [green]{value}[/]");
    }

    static void OutputHeader(string title)
    {
        var rule = new Rule($"[bold blue]{title}[/]").LeftJustified();

        AnsiConsole.Write(rule);
    }
}
