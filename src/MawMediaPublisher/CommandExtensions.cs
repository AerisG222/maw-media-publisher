using System.Text;
using CliWrap;

namespace MawMediaPublisher;

class ExternalToolException(string tool, int exitCode, string error)
    : Exception(string.IsNullOrWhiteSpace(error)
        ? $"{tool} failed w/ exit code {exitCode}"
        : $"{tool} failed w/ exit code {exitCode}: {error}")
{
    public string Tool { get; } = tool;
    public int ExitCode { get; } = exitCode;
}

static class CommandExtensions
{
    const int MAX_ERROR_LINES = 10;

    // runs the command and, if it fails, throws w/ the last lines written to stderr so the failure is actionable.
    // (CliWrap's default exception only includes the exit code and command line, not the reason)
    public static async Task ExecuteWithErrorsAsync(this Command command, CancellationToken token = default)
    {
        var stderr = new StringBuilder();

        var result = await command
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(token);

        if (result.ExitCode != 0)
        {
            throw new ExternalToolException(Path.GetFileName(command.TargetFilePath), result.ExitCode, Tail(stderr.ToString()));
        }
    }

    internal static string Tail(string output)
    {
        var lines = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .TakeLast(MAX_ERROR_LINES);

        return string.Join(Environment.NewLine, lines);
    }
}
