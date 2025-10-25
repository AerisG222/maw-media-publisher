using System.ComponentModel;
using MawMediaPublisher.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MawMediaPublisher.Commands;

internal sealed class FullProcessCommand
    : Command<FullProcessCommand.Settings>
{
    const int STATUS_SUCCESS = 0;
    const int STATUS_USER_CANCELLED = 1;

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

    public override int Execute(
        CommandContext context,
        Settings settings
    )
    {
        var category = new Category(
            settings.CategoryName,
            settings.MediaPath,
            settings.EffectiveDate
        );

        if (settings.Interactive)
        {
            OutputHeader("Parameters");
            OutputVariable(" Category Name", category.Name);
            OutputVariable("Effective Date", category.EffectiveDate.ToString("yyyy-mm-dd"));
            OutputVariable("  Media Source", category.SourceDirectory);
            OutputVariable(" Base Web Path", category.BaseWebPath);

            AnsiConsole.WriteLine();

            if (!AnsiConsole.Prompt(new ConfirmationPrompt("Continue?")))
            {
                return STATUS_USER_CANCELLED;
            }
        }

        // do work here

        return STATUS_SUCCESS;
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
