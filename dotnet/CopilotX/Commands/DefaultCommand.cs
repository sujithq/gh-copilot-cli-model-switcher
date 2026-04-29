using System.ComponentModel;
using CopilotX.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CopilotX.Commands;

/// <summary>Shows or sets the default profile.</summary>
public sealed class DefaultCommand : Command<DefaultCommand.Settings>
{
    private readonly ConfigService _config;

    public DefaultCommand(ConfigService config) => _config = config;

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[profile]")]
        [Description("Profile name to set as default (omit to show current default)")]
        public string? Profile { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var config = _config.Load();

        if (string.IsNullOrWhiteSpace(settings.Profile))
        {
            var def = config.DefaultProfile ?? "default";
            AnsiConsole.MarkupLine($"\n[bold]Default profile: [green]{Markup.Escape(def)}[/][/]\n");

            var p = ConfigService.GetProfile(config, def);
            if (p is not null)
            {
                AnsiConsole.MarkupLine($"  Type:  [cyan]{p.Type.ToString().ToLowerInvariant()}[/]");
                AnsiConsole.MarkupLine(p.Type == Models.ProfileType.Copilot
                    ? $"  Model: {Markup.Escape(p.Model ?? "auto")}"
                    : $"  URL:   [grey]{Markup.Escape(p.BaseUrl ?? "(none)")}[/]\n  Model: {Markup.Escape(p.Model ?? "(none)")}");
            }
            else
            {
                AnsiConsole.MarkupLine($"  [yellow]Profile [bold]{Markup.Escape(def)}[/] not found in config.[/]");
            }
            AnsiConsole.WriteLine();
            return 0;
        }

        var target = settings.Profile.Trim();
        if (ConfigService.GetProfile(config, target) is null)
        {
            AnsiConsole.MarkupLine(
                $"[red]Profile [bold]{Markup.Escape(target)}[/] not found. " +
                "Run [bold]copilotx list[/] to see available profiles.[/]");
            return 1;
        }

        config.DefaultProfile = target;
        _config.Save(config);
        AnsiConsole.MarkupLine($"[green]✔ Default profile set to: [bold]{Markup.Escape(target)}[/][/]");
        return 0;
    }
}
