using System.ComponentModel;
using CopilotX.Models;
using CopilotX.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CopilotX.Commands;

/// <summary>Lists all configured profiles.</summary>
public sealed class ListCommand : Command<ListCommand.Settings>
{
    private readonly ConfigService _config;

    public ListCommand(ConfigService config) => _config = config;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        [Description("Output profiles as JSON")]
        [DefaultValue(false)]
        public bool Json { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var config = _config.Load();

        if (settings.Json)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(
                config.Profiles,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
                });
            AnsiConsole.WriteLine(json);
            return 0;
        }

        if (config.Profiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No profiles configured. Run [bold]copilotx add[/] to create one.[/]");
            return 0;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Name[/]"))
            .AddColumn(new TableColumn("[bold]Type[/]"))
            .AddColumn(new TableColumn("[bold]Model[/]"))
            .AddColumn(new TableColumn("[bold]Base URL[/]"))
            .AddColumn(new TableColumn("[bold]Auth[/]"))
            .AddColumn(new TableColumn("[bold]Status[/]"));

        foreach (var p in config.Profiles)
        {
            var isLast = p.Name == config.LastUsed;
            var status = isLast ? "[green]✔ last used[/]" : string.Empty;
            var typeLabel = p.Type == ProfileType.Copilot ? "[cyan]copilot[/]" : "[blue]byok[/]";
            var model = p.Model ?? "auto";
            var baseUrl = p.BaseUrl ?? "-";
            var auth = p.ApiKeyEnv is not null
                ? $"[grey]${p.ApiKeyEnv}[/]"
                : p.ApiKey is not null
                ? "[grey]inline key[/]"
                : "-";

            table.AddRow(
                Markup.Escape(p.Name),
                typeLabel,
                Markup.Escape(model),
                Markup.Escape(baseUrl),
                auth,
                status);
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        return 0;
    }
}
