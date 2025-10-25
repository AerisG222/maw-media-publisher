using System.ComponentModel;
using MawMediaPublisher.Finder;
using MawMediaPublisher.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MawMediaPublisher.Commands;

internal sealed class FullProcessCommand
    : AsyncCommand<FullProcessCommand.Settings>
{
    const int STATUS_SUCCESS = 0;
    const int STATUS_USER_CANCELLED = 1;
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

        [CommandOption("-i|--interactive", false)]
        [Description("Interactive mode - illustrate steps and prompt to continue before executing anything.")]
        public bool Interactive { get; init; }
    }

    public async override Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings
    )
    {
        var category = new Category(
            settings.CategoryName,
            settings.MediaPath,
            settings.EffectiveDate
        );

        if (settings.Interactive && !PrintParametersAndContinue(category))
        {
            return STATUS_USER_CANCELLED;
        }

        var mediaFinder = new MediaFinder();
        var foundFiles = mediaFinder.FindMedia(category.SourceDirectory);

        if (settings.Interactive && !PrintMediaToProcessAndContinue(foundFiles))
        {
            return STATUS_USER_CANCELLED;
        }

        category.Media = foundFiles.Media;

        await ProcessCategoryMedia(category);

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
                    await ProcessMedia(media);

                    lock (_lock)
                    {
                        task.Increment(pctPerFile);
                    }
                });
            });
    }

    static async Task ProcessMedia(MediaFile file)
    {
        await Task.Delay(1000);
    }

    static bool PrintParametersAndContinue(Category category)
    {
        OutputHeader("Parameters");
        OutputVariable(" Category Name", category.Name);
        OutputVariable("Effective Date", category.EffectiveDate.ToString("yyyy-MM-dd"));
        OutputVariable("  Media Source", category.SourceDirectory);
        OutputVariable(" Base Web Path", category.BaseWebPath);

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

    static bool ContinuePrompt()
    {
        return AnsiConsole.Prompt(new ConfirmationPrompt("Continue?"));
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
