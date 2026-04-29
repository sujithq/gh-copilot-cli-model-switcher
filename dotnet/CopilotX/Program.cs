using Spectre.Console;
using System.Diagnostics;

namespace CopilotX;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return 0;
        }

        var command = args[0].ToLower();
        var remainingArgs = args.Skip(1).ToArray();

        return command switch
        {
            "list" => ListCommand(),
            "use" => UseCommand(remainingArgs),
            "last" => LastCommand(remainingArgs),
            "default" => DefaultCommand(remainingArgs),
            "add" => AddCommand(),
            "help" or "--help" or "-h" => ShowHelp(),
            _ => ShowHelp()
        };
    }

    static int ShowHelp()
    {
        AnsiConsole.Write(
            new FigletText("CopilotX")
                .LeftJustified()
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[bold]A lightweight CLI wrapper for GitHub Copilot CLI[/]\n");

        var table = new Table();
        table.AddColumn("Command");
        table.AddColumn("Description");

        table.AddRow("[cyan]list[/]", "List all available profiles");
        table.AddRow("[cyan]use <profile> [[args...]][/]", "Switch to a specific profile and run gh copilot");
        table.AddRow("[cyan]last [[args...]][/]", "Use the last used profile and run gh copilot");
        table.AddRow("[cyan]default [[args...]][/]", "Use the default Copilot profile");
        table.AddRow("[cyan]add[/]", "Add or update a profile interactively");
        table.AddRow("[cyan]help[/]", "Show this help message");

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[dim]Examples:[/]");
        AnsiConsole.MarkupLine("  copilotx list");
        AnsiConsole.MarkupLine("  copilotx use azure-gpt suggest \"create a function\"");
        AnsiConsole.MarkupLine("  copilotx last");
        AnsiConsole.MarkupLine("  copilotx add");

        return 0;
    }

    static int ListCommand()
    {
        var profiles = ConfigManager.ListProfiles();
        var lastUsed = ConfigManager.GetLastUsed();

        AnsiConsole.MarkupLine("[bold blue]Available profiles:[/]\n");

        var table = new Table();
        table.AddColumn("");
        table.AddColumn("Name");
        table.AddColumn("Type");
        table.AddColumn("Base URL");
        table.AddColumn("Model");

        foreach (var profile in profiles)
        {
            var marker = profile.Name == lastUsed ? "[green]*[/]" : " ";
            var baseUrl = profile.BaseUrl ?? "N/A";
            var model = profile.Model ?? "N/A";

            table.AddRow(
                marker,
                $"[cyan]{profile.Name}[/]",
                profile.Type,
                baseUrl,
                model
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[dim]* = last used[/]");
        AnsiConsole.MarkupLine($"[dim]Config file: {ConfigManager.GetConfigFile()}[/]");

        return 0;
    }

    static int UseCommand(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: Profile name required[/]");
            AnsiConsole.MarkupLine("Usage: copilotx use <profile> [args...]");
            return 1;
        }

        var profileName = args[0];
        var copilotArgs = args.Skip(1).ToArray();

        return ExecuteWithProfile(profileName, copilotArgs);
    }

    static int LastCommand(string[] args)
    {
        var lastUsed = ConfigManager.GetLastUsed();

        if (string.IsNullOrEmpty(lastUsed))
        {
            AnsiConsole.MarkupLine("[red]Error: No profile has been used yet[/]");
            return 1;
        }

        return ExecuteWithProfile(lastUsed, args);
    }

    static int DefaultCommand(string[] args)
    {
        return ExecuteWithProfile("default", args);
    }

    static int ExecuteWithProfile(string profileName, string[] copilotArgs)
    {
        var profile = ConfigManager.GetProfile(profileName);

        if (profile == null)
        {
            AnsiConsole.MarkupLine($"[red]Error: Profile '{profileName}' not found[/]");
            AnsiConsole.MarkupLine("Use 'copilotx list' to see available profiles.");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Using profile:[/] {profile.Name} ([dim]{profile.Type}[/])");

        SetEnvironmentForProfile(profile);
        ConfigManager.SetLastUsed(profileName);

        var startInfo = new ProcessStartInfo
        {
            FileName = "gh",
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("copilot");
        foreach (var arg in copilotArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        try
        {
            var process = Process.Start(startInfo);
            if (process == null)
            {
                AnsiConsole.MarkupLine("[red]Error: Failed to start gh copilot[/]");
                return 1;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error executing gh copilot: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[dim]Make sure GitHub Copilot CLI is installed: gh extension install github/gh-copilot[/]");
            return 1;
        }
    }

    static void SetEnvironmentForProfile(Profile profile)
    {
        if (profile.Type == "copilot")
        {
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_BASE_URL", null);
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_API_KEY", null);
            Environment.SetEnvironmentVariable("COPILOT_MODEL", null);
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_TYPE", null);
        }
        else if (profile.Type == "byok" || profile.Type == "proxy")
        {
            if (!string.IsNullOrEmpty(profile.BaseUrl))
            {
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_BASE_URL", profile.BaseUrl);
            }

            if (!string.IsNullOrEmpty(profile.Model))
            {
                Environment.SetEnvironmentVariable("COPILOT_MODEL", profile.Model);
            }

            if (!string.IsNullOrEmpty(profile.ApiKeyEnv))
            {
                var apiKey = Environment.GetEnvironmentVariable(profile.ApiKeyEnv);
                if (!string.IsNullOrEmpty(apiKey))
                {
                    Environment.SetEnvironmentVariable("COPILOT_PROVIDER_API_KEY", apiKey);
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning: Environment variable {profile.ApiKeyEnv} is not set[/]");
                }
            }
            else if (!string.IsNullOrEmpty(profile.ApiKey))
            {
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_API_KEY", profile.ApiKey);
            }

            if (!string.IsNullOrEmpty(profile.ProviderType))
            {
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_TYPE", profile.ProviderType);
            }
        }
    }

    static int AddCommand()
    {
        AnsiConsole.MarkupLine("[bold blue]Add/Update Profile[/]\n");

        var name = AnsiConsole.Ask<string>("Profile [cyan]name[/]:");
        if (string.IsNullOrWhiteSpace(name))
        {
            AnsiConsole.MarkupLine("[red]Error: Profile name cannot be empty[/]");
            return 1;
        }

        var type = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Profile [cyan]type[/]:")
                .AddChoices(new[] { "copilot", "byok", "proxy" }));

        var profile = new Profile
        {
            Name = name.Trim(),
            Type = type
        };

        if (type == "byok" || type == "proxy")
        {
            profile.BaseUrl = AnsiConsole.Ask<string>("Base [cyan]URL[/]:", string.Empty);
            profile.Model = AnsiConsole.Ask<string>("[cyan]Model[/]:", string.Empty);

            var apiKeyChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("API Key [cyan]source[/]:")
                    .AddChoices(new[] { "env", "direct", "none" }));

            if (apiKeyChoice == "env")
            {
                profile.ApiKeyEnv = AnsiConsole.Ask<string>("Environment variable [cyan]name[/]:", string.Empty);
            }
            else if (apiKeyChoice == "direct")
            {
                profile.ApiKey = AnsiConsole.Prompt(
                    new TextPrompt<string>("API [cyan]Key[/]:")
                        .Secret());
            }

            profile.ProviderType = AnsiConsole.Ask<string>("Provider [cyan]type[/] (optional):", string.Empty);
        }

        if (ConfigManager.AddProfile(profile))
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Profile '{name}' added successfully!");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Error: Failed to add profile[/]");
            return 1;
        }

        return 0;
    }
}
