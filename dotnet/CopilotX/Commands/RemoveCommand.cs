using System.ComponentModel;
using CopilotX.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CopilotX.Commands;

/// <summary>Removes a profile from the configuration.</summary>
public sealed class RemoveCommand : Command<RemoveCommand.Settings>
{
    private readonly ConfigService _config;

    public RemoveCommand(ConfigService config) => _config = config;

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<profile>")]
        [Description("Profile name to remove")]
        public string Profile { get; set; } = string.Empty;
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

        config.Profiles.Remove(profile);

        if (config.LastUsed == settings.Profile)
            config.LastUsed = config.Profiles.FirstOrDefault()?.Name;

        if (config.DefaultProfile == settings.Profile)
            config.DefaultProfile = config.Profiles.FirstOrDefault()?.Name;

        _config.Save(config);
        AnsiConsole.MarkupLine($"[green]✔ Profile [bold]{Markup.Escape(settings.Profile)}[/] removed.[/]");
        return 0;
    }
}
