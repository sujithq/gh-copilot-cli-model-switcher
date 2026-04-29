using System.ComponentModel;
using System.Diagnostics;
using CopilotX.Models;
using CopilotX.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CopilotX.Commands;

/// <summary>Switches to a profile and optionally launches GitHub Copilot CLI.</summary>
public sealed class UseCommand : Command<UseCommand.Settings>
{
    private readonly ConfigService _config;

    public UseCommand(ConfigService config) => _config = config;

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<profile>")]
        [Description("Profile name to activate")]
        public string Profile { get; set; } = string.Empty;

        [CommandOption("--dry-run")]
        [Description("Print what would happen without launching copilot")]
        [DefaultValue(false)]
        public bool DryRun { get; set; }
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

        if (settings.DryRun)
        {
            AnsiConsole.MarkupLine($"[green]✔ Would switch to profile: [bold]{Markup.Escape(profile.Name)}[/][/]");
            foreach (var key in unset)
                AnsiConsole.MarkupLine($"  [grey]Would unset: {key}[/]");
            foreach (var (key, val) in set)
                AnsiConsole.MarkupLine($"  [grey]Would set: {key}={Markup.Escape(val)}[/]");
            AnsiConsole.MarkupLine("[yellow]  (dry-run: copilot not launched)[/]");
            return 0;
        }

        // Apply environment variables
        foreach (var key in unset)
            Environment.SetEnvironmentVariable(key, null);
        foreach (var (key, val) in set)
            Environment.SetEnvironmentVariable(key, val);

        // Persist last used
        config.LastUsed = profile.Name;
        _config.Save(config);

        AnsiConsole.MarkupLine($"[green]✔ Switched to profile: [bold]{Markup.Escape(profile.Name)}[/][/]");

        var copilotBin = Environment.GetEnvironmentVariable("COPILOT_BIN") ?? "copilot";

        try
        {
            var psi = new ProcessStartInfo(copilotBin)
            {
                UseShellExecute = false,
                CreateNoWindow = false,
            };

            // Pass through all current env vars (including the ones we just set)
            foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string k && entry.Value is string v)
                    psi.Environment[k] = v;
            }

            var proc = Process.Start(psi);
            if (proc is null)
            {
                AnsiConsole.MarkupLine($"[red]Failed to start process: {Markup.Escape(copilotBin)}[/]");
                return 1;
            }

            proc.WaitForExit();
            return proc.ExitCode;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[red]Failed to launch [bold]{Markup.Escape(copilotBin)}[/]. " +
                "Is GitHub Copilot CLI installed?[/]\n" +
                $"  [grey]{Markup.Escape(ex.Message)}[/]\n\n" +
                $"Tip: use [bold]copilotx env {Markup.Escape(profile.Name)}[/] to export variables for manual use.");
            return 1;
        }
    }
}
