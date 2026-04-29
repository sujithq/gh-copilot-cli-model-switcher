using System.ComponentModel;
using CopilotX.Models;
using CopilotX.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CopilotX.Commands;

/// <summary>Prints shell export commands for a profile so the user can eval them.</summary>
public sealed class EnvCommand : Command<EnvCommand.Settings>
{
    private readonly ConfigService _config;

    public EnvCommand(ConfigService config) => _config = config;

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<profile>")]
        [Description("Profile name")]
        public string Profile { get; set; } = string.Empty;

        [CommandOption("--shell")]
        [Description("Shell syntax: bash, fish, powershell (default: bash)")]
        [DefaultValue("bash")]
        public string Shell { get; set; } = "bash";
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var config = _config.Load();
        var profile = ConfigService.GetProfile(config, settings.Profile);

        if (profile is null)
        {
            AnsiConsole.MarkupLine(
                $"[red]Profile [bold]{Markup.Escape(settings.Profile)}[/] not found. " +
                "Run [bold]copilotx list[/] to see available profiles.[/]");
            return 1;
        }

        var (set, unset) = ConfigService.BuildEnvForProfile(profile);

        switch (settings.Shell.ToLowerInvariant())
        {
            case "fish":
                foreach (var key in unset)
                    Console.WriteLine($"set -e {key}");
                foreach (var (key, val) in set)
                    Console.WriteLine($"set -x {key} '{EscapeShellValue(val)}'");
                break;

            case "powershell":
                foreach (var key in unset)
                    Console.WriteLine($"Remove-Item Env:{key} -ErrorAction SilentlyContinue");
                foreach (var (key, val) in set)
                    Console.WriteLine($"$Env:{key} = '{EscapePowerShellValue(val)}'");
                break;

            default: // bash / sh
                foreach (var key in unset)
                    Console.WriteLine($"unset {key}");
                foreach (var (key, val) in set)
                    Console.WriteLine($"export {key}='{EscapeShellValue(val)}'");
                break;
        }

        return 0;
    }

    private static string EscapeShellValue(string value) =>
        value.Replace("'", "'\\''");

    private static string EscapePowerShellValue(string value) =>
        value.Replace("'", "''");
}
