using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using MawMediaPublisher.Metadata;
using MawMediaPublisher.Finder;
using MawMediaPublisher.Models;
using MawMediaPublisher.Scale;
using MawMediaPublisher.Sql;
using MawMediaPublisher.Deploy;

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
    static readonly LocalDeployer _localDeployer = new();
    static readonly ProductionDeployer _productionDeployer = new();

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

        [CommandOption("-s|--server", false)]
        [Description("Remote Server - hosts production instance of media.mikeandwan.us (ssh key should be configured first!)")]
        [DefaultValue("chocobo")]
        public string RemoteServer { get; init; } = "";

        [CommandOption("-u|--user", false)]
        [Description("Remote Username - service account on the remote server hosting media.mikeandwan.us)")]
        [DefaultValue("svc_maw_media")]
        public string RemoteUsername { get; init; } = "";

        [CommandOption("--ssh-private-key-file", false)]
        [Description("SSH Private Key File - Path to the private key to use when connecting via SSH to the remote server")]
        [DefaultValue("/home/mmorano/.ssh/id_rsa")]
        public string SshPrivateKeyFile { get; init; } = "";
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
            settings.RemoteAssetRoot,
            settings.RemoteServer,
            settings.RemoteUsername,
            settings.SshPrivateKeyFile
        );

        if (Directory.Exists(category.LocalMediaPath))
        {
            AnsiConsole.MarkupLineInterpolated($"[bold red]!!** Asset directory {category.LocalMediaPath} already exists.  Please choose a unique directory name **!![/]");

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

        await _localDeployer.Deploy(category);

        if (!ContinuePrompt("Production Deploy?"))
        {
            return STATUS_USER_CANCELLED;
        }

        await _productionDeployer.Deploy(category);

        if (!ContinuePrompt("Remote Asset Archive?"))
        {
            return STATUS_USER_CANCELLED;
        }

        //await RemoteArchive(category);

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

    static bool PrintParametersAndContinue(Category category)
    {
        OutputHeader("Parameters");
        OutputVariable("    Category Name", category.Name);
        OutputVariable("   Effective Date", category.EffectiveDate.ToString("yyyy-MM-dd"));
        OutputVariable("     Media Source", category.SourceDirectory);
        OutputVariable("     Base Web URL", category.BaseWebUrl);
        OutputVariable("            Roles", string.Join(" ", category.Roles));
        OutputVariable("  Local Year Path", category.LocalYearPath);
        OutputVariable(" Local Media Path", category.LocalMediaPath);
        OutputVariable(" Remote Year Path", category.RemoteYearPath);
        OutputVariable("Remote Media Path", category.RemoteMediaPath);
        OutputVariable("    Remote Server", category.RemoteServer);
        OutputVariable("  Remote Username", category.RemoteUsername);
        OutputVariable("  Private SSH Key", category.SshPrivateKeyFile);

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
