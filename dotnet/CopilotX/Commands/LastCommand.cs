using System.ComponentModel;
using System.Diagnostics;
using CopilotX.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CopilotX.Commands;

/// <summary>Shows the last-used profile, or re-activates it to launch Copilot CLI.</summary>
public sealed class LastCommand : Command<LastCommand.Settings>
{
    private readonly ConfigService _config;

    public LastCommand(ConfigService config) => _config = config;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--use")]
        [Description("Re-activate the last used profile and launch copilot")]
        [DefaultValue(false)]
        public bool Use { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var config = _config.Load();

        if (string.IsNullOrEmpty(config.LastUsed))
        {
            AnsiConsole.MarkupLine("[yellow]No profile has been used yet. Run [bold]copilotx use <profile>[/] first.[/]");
            return 0;
        }

        var profile = ConfigService.GetProfile(config, config.LastUsed);
        if (profile is null)
        {
            AnsiConsole.MarkupLine($"[yellow]Last used profile [bold]{Markup.Escape(config.LastUsed)}[/] no longer exists.[/]");
            return 0;
        }

        if (!settings.Use)
        {
            AnsiConsole.MarkupLine($"\n[bold]Last used profile: [green]{Markup.Escape(config.LastUsed)}[/][/]\n");
            AnsiConsole.MarkupLine($"  Type:  [cyan]{profile.Type.ToString().ToLowerInvariant()}[/]");
            if (profile.Type == Models.ProfileType.Copilot)
            {
                AnsiConsole.MarkupLine($"  Model: {Markup.Escape(profile.Model ?? "auto")}");
            }
            else
            {
                AnsiConsole.MarkupLine($"  URL:   [grey]{Markup.Escape(profile.BaseUrl ?? "(none)")}[/]");
                AnsiConsole.MarkupLine($"  Model: {Markup.Escape(profile.Model ?? "(none)")}");
            }
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("  [grey]Run [bold]copilotx last --use[/] to re-activate it.[/]");
            AnsiConsole.WriteLine();
            return 0;
        }

        // --use: re-activate
        var (set, unset) = ConfigService.BuildEnvForProfile(profile);
        foreach (var key in unset)
            Environment.SetEnvironmentVariable(key, null);
        foreach (var (key, val) in set)
            Environment.SetEnvironmentVariable(key, val);

        _config.Save(config);

        AnsiConsole.MarkupLine($"[green]✔ Re-activating profile: [bold]{Markup.Escape(profile.Name)}[/][/]");

        var copilotBin = Environment.GetEnvironmentVariable("COPILOT_BIN") ?? "copilot";
        try
        {
            var proc = Process.Start(new ProcessStartInfo(copilotBin) { UseShellExecute = false });
            if (proc is null) return 1;
            proc.WaitForExit();
            return proc.ExitCode;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to launch [bold]{Markup.Escape(copilotBin)}[/]: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }
}
