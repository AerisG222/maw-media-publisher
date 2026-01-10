using Spectre.Console;
using Spectre.Console.Cli;

namespace MawMediaPublisher.Commands;

public class DirectoryExistsAttribute
    : ParameterValidationAttribute
{
    public DirectoryExistsAttribute(string errorMessage)
        : base(errorMessage)
    {
        // do nothing
    }

    public override ValidationResult Validate(CommandParameterContext ctx)
    {
        if (typeof(string) != ctx.Value?.GetType())
        {
            return ValidationResult.Error("A string must be used to specify the directory!");
        }

        var dir = ctx.Value as string;

        if (!Directory.Exists(dir))
        {
            return ValidationResult.Error($"Directory {dir} does not exist!");
        }

        return ValidationResult.Success();
    }
}
