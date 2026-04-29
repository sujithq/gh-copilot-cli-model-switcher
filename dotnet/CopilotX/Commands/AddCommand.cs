using System.ComponentModel;
using CopilotX.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using ModelProfile = CopilotX.Models.Profile;
using ProfileType = CopilotX.Models.ProfileType;

namespace CopilotX.Commands;

/// <summary>Interactively adds a new model profile.</summary>
public sealed class AddCommand : Command<AddCommand.Settings>
{
    private readonly ConfigService _config;

    public AddCommand(ConfigService config) => _config = config;

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[name]")]
        [Description("Profile name (will be prompted if omitted)")]
        public string? Name { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var config = _config.Load();

        // Resolve name
        string name;
        if (!string.IsNullOrWhiteSpace(settings.Name))
        {
            name = settings.Name.Trim();
            if (ConfigService.GetProfile(config, name) is not null)
            {
                AnsiConsole.MarkupLine($"[red]Profile [bold]{Markup.Escape(name)}[/] already exists.[/]");
                return 1;
            }
        }
        else
        {
            name = AnsiConsole.Prompt(
                new TextPrompt<string>("[bold]Profile name:[/]")
                    .Validate(v =>
                    {
                        if (string.IsNullOrWhiteSpace(v)) return ValidationResult.Error("Name cannot be empty.");
                        if (ConfigService.GetProfile(config, v.Trim()) is not null)
                            return ValidationResult.Error($"Profile \"{v.Trim()}\" already exists.");
                        return ValidationResult.Success();
                    }));
            name = name.Trim();
        }

        // Profile type
        var profileType = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Profile type:[/]")
                .AddChoices("copilot", "byok"));

        var profile = new ModelProfile { Name = name };

        if (profileType == "copilot")
        {
            profile.Type = ProfileType.Copilot;
            profile.Model = AnsiConsole.Ask("[bold]Model name[/] (e.g. gpt-4o, auto):", "auto");
        }
        else
        {
            profile.Type = ProfileType.Byok;

            profile.BaseUrl = AnsiConsole.Prompt(
                new TextPrompt<string>("[bold]Provider base URL:[/]")
                    .Validate(v => string.IsNullOrWhiteSpace(v)
                        ? ValidationResult.Error("Base URL cannot be empty.")
                        : ValidationResult.Success()));

            profile.Model = AnsiConsole.Prompt(
                new TextPrompt<string>("[bold]Model name:[/]")
                    .Validate(v => string.IsNullOrWhiteSpace(v)
                        ? ValidationResult.Error("Model cannot be empty.")
                        : ValidationResult.Success()));

            var authMethod = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]API key method:[/]")
                    .AddChoices("environment variable", "inline value", "none"));

            if (authMethod == "environment variable")
            {
                profile.ApiKeyEnv = AnsiConsole.Prompt(
                    new TextPrompt<string>("[bold]Environment variable name[/] (e.g. AZURE_OPENAI_KEY):")
                        .Validate(v => string.IsNullOrWhiteSpace(v)
                            ? ValidationResult.Error("Variable name cannot be empty.")
                            : ValidationResult.Success()));
            }
            else if (authMethod == "inline value")
            {
                profile.ApiKey = AnsiConsole.Prompt(
                    new TextPrompt<string>("[bold]API key value[/] (stored in plain text):")
                        .Secret()
                        .Validate(v => string.IsNullOrWhiteSpace(v)
                            ? ValidationResult.Error("API key cannot be empty.")
                            : ValidationResult.Success()));
            }

            var providerType = AnsiConsole.Ask("[bold]Provider type[/] (optional, e.g. azure, openai):", string.Empty);
            if (!string.IsNullOrWhiteSpace(providerType))
                profile.ProviderType = providerType.Trim();
        }

        config.Profiles.Add(profile);
        _config.Save(config);

        AnsiConsole.MarkupLine($"\n[green]✔ Profile [bold]{Markup.Escape(name)}[/] added successfully.[/]");
        AnsiConsole.MarkupLine($"  [grey]Run [bold]copilotx use {Markup.Escape(name)}[/] to activate it.[/]\n");

        return 0;
    }
}
