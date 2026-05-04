using Spectre.Console;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CopilotX;

class Program
{
    static readonly string[] DefaultMcpCompatServers = ["foundry-mcp", "context7", "msx-mcp", "azure", "workiq", "powerbi-remote"];

    static string GetAzureCliCommand()
    {
        return OperatingSystem.IsWindows() ? "az.cmd" : "az";
    }

    static string EscapeMarkup(string value)
    {
        return Markup.Escape(value ?? string.Empty);
    }

    static string QuoteForCmd(string value)
    {
        return $"\"{(value ?? string.Empty).Replace("\"", "\\\"")}\"";
    }

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
            "manage" => ManageCommand(),
            "mcp-compat" => McpCompatCommand(remainingArgs),
            "remove" => RemoveCommand(remainingArgs),
            "use" => UseCommand(remainingArgs),
            "last" => LastCommand(remainingArgs),
            "default" => DefaultCommand(remainingArgs),
            "add" => AddCommand(),
            "import-foundry" => ImportFoundryCommand(remainingArgs).GetAwaiter().GetResult(),
            "help" or "--help" or "-h" => ShowHelp(),
            _ => ShowHelp()
        };
    }

    static int ShowHelp()
    {
        AnsiConsole.Write(
            new FigletText("copilot-byok-model-switcher")
                .LeftJustified()
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[bold]A lightweight CLI wrapper for GitHub Copilot CLI[/]\n");

        var table = new Table();
        table.AddColumn("Command");
        table.AddColumn("Description");

        table.AddRow("[cyan]list[/]", "List all available profiles (read-only)");
        table.AddRow("[cyan]manage[/]", "Interactive profile management (Use/Remove/Add/Import/MCP)");
        table.AddRow("[cyan]mcp-compat <profile> [[--action set|reset|all|none]][/]", "Set or reset MCP compatibility servers for an Azure BYOK/proxy profile");
        table.AddRow("[cyan]remove [[profiles...]][/]", "Remove one or more profiles (interactive multi-select)");
        table.AddRow("[cyan]use <profile> [[args...]][/]", "Switch to a specific profile and run gh copilot");
        table.AddRow("[cyan]last [[args...]][/]", "Use the last used profile and run gh copilot");
        table.AddRow("[cyan]default [[args...]][/]", "Use the default Copilot profile");
        table.AddRow("[cyan]add[/]", "Add or update a profile interactively");
        table.AddRow("[cyan]import-foundry [[options]][/]", "Import profiles from Foundry/Azure OpenAI deployments");
        table.AddRow("[cyan]help[/]", "Show this help message");

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[bold]Passthrough flags[/] [dim](forwarded to gh copilot, applies to use / last / default):[/]");
        var flagTable = new Table();
        flagTable.AddColumn("Flag");
        flagTable.AddColumn("Description");
        flagTable.AddRow("[cyan]-p / --prompt <text>[/]", "Run non-interactively with a prompt");
        flagTable.AddRow("[cyan]--allow-all-tools[/]", "Allow all tools (auto-injected in -p mode)");
        flagTable.AddRow("[cyan]--allow-all / --yolo[/]", "Allow all tools and operations");
        flagTable.AddRow("[cyan]--allow-tool <name>[/]", "Allow only a specific named tool");
        flagTable.AddRow("[cyan]--deny-tool <name>[/]", "Deny a specific named tool");
        flagTable.AddRow("[cyan]--disable-mcp-server <name>[/]", "Disable a named MCP server");
        flagTable.AddRow("[cyan]--disable-builtin-mcps[/]", "Disable all built-in MCP servers");
        AnsiConsole.Write(flagTable);

        AnsiConsole.MarkupLine("\n[bold]import-foundry options:[/]");
        var ifTable = new Table();
        ifTable.AddColumn("Option");
        ifTable.AddColumn("Description");
        ifTable.AddRow("[cyan]--account <name>[/]", "Limit import to a single named account");
        ifTable.AddRow("[cyan]--resource-group <rg>[/]", "Resource group of the account (required with --account)");
        ifTable.AddRow("[cyan]--subscription <id|name>[/]", "Scope discovery to a specific subscription");
        ifTable.AddRow("[cyan]--mode each|all[/]", "Prompt per deployment (each) or add all without prompts (all)");
        ifTable.AddRow("[cyan]--all[/]", "Shorthand for --mode all");
        ifTable.AddRow("[cyan]--max-output-tokens <n>[/]", "Set max output tokens on imported profiles");
        ifTable.AddRow("[cyan]--max-prompt-tokens <n>[/]", "Set max prompt tokens on imported profiles");
        AnsiConsole.Write(ifTable);

        AnsiConsole.MarkupLine("\n[bold]Azure BYOK MCP compat mode[/]");
        AnsiConsole.MarkupLine("  On first interactive launch of an Azure BYOK profile, you are prompted to select which MCP servers to");
        AnsiConsole.MarkupLine("  disable (to avoid provider tool-count limits). The selection is saved as [cyan]mcpCompatServers[/] on the");
        AnsiConsole.MarkupLine("  profile and reused on every subsequent run. Remove the field from config to re-prompt.");
        AnsiConsole.MarkupLine("  Default candidates: [dim]foundry-mcp, context7, msx-mcp, azure, workiq, powerbi-remote[/]");
        AnsiConsole.MarkupLine("  Set [cyan]CBMS_DISABLE_MCP_COMPAT=off[/] to skip compat mode entirely.");

        AnsiConsole.MarkupLine("\n[dim]Examples:[/]");
        AnsiConsole.MarkupLine("  copilot-byok-model-switcher list");
        AnsiConsole.MarkupLine("  copilot-byok-model-switcher manage");
        AnsiConsole.MarkupLine("  copilot-byok-model-switcher mcp-compat azure-gpt --action reset");
        AnsiConsole.MarkupLine("  copilot-byok-model-switcher remove");
        AnsiConsole.MarkupLine("  copilot-byok-model-switcher remove azure-gpt ollama-local");
        AnsiConsole.MarkupLine("  copilot-byok-model-switcher use azure-gpt");
        AnsiConsole.MarkupLine("  copilot-byok-model-switcher use azure-gpt -p \"create a function\"");
        AnsiConsole.MarkupLine("  copilot-byok-model-switcher use azure-gpt -p \"fix the failing tests\"");
        AnsiConsole.MarkupLine("  copilot-byok-model-switcher use azure-gpt -p \"refactor this\" --allow-tool=write");
        AnsiConsole.MarkupLine("  copilot-byok-model-switcher last -p \"explain this code\"");
        AnsiConsole.MarkupLine("  copilot-byok-model-switcher default");
        AnsiConsole.MarkupLine("  copilot-byok-model-switcher add");
        AnsiConsole.MarkupLine("  copilot-byok-model-switcher import-foundry");
        AnsiConsole.MarkupLine("  copilot-byok-model-switcher import-foundry --all");
        AnsiConsole.MarkupLine("  copilot-byok-model-switcher import-foundry --account myfoundry --resource-group my-rg --all");
        AnsiConsole.MarkupLine("  copilot-byok-model-switcher import-foundry --subscription 00000000-0000-0000-0000-000000000000 --all");

        return 0;
    }

    static int ListCommand()
    {
        var profiles = ConfigManager.ListProfiles();
        var lastUsed = ConfigManager.GetLastUsed();

        AnsiConsole.MarkupLine("[bold blue]Available profiles:[/]\n");

        var table = new Table();
        table.AddColumn("#");
        table.AddColumn("");
        table.AddColumn("Name");
        table.AddColumn("Type");
        table.AddColumn("Base URL");
        table.AddColumn("Model");
        table.AddColumn("Token Limits");

        var profileList = profiles.ToList();
        for (int i = 0; i < profileList.Count; i++)
        {
            var profile = profileList[i];
            var marker = profile.Name == lastUsed ? "[green]*[/]" : " ";
            var baseUrl = profile.BaseUrl ?? "N/A";
            var model = profile.Model ?? "N/A";
            var tokenInfo = FormatProfileTokenInfo(profile);

            table.AddRow(
                $"[dim]{i + 1}[/]",
                marker,
                $"[cyan]{profile.Name}[/]",
                profile.Type,
                baseUrl,
                model,
                tokenInfo
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[dim]* = last used[/]");
        AnsiConsole.MarkupLine($"[dim]Config file: {ConfigManager.GetConfigFile()}[/]");

        return 0;
    }

    static int ManageCommand()
    {
        var profileList = ConfigManager.ListProfiles();
        if (profileList.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No profiles found.[/]");
            return 0;
        }

        var lastUsed = ConfigManager.GetLastUsed();
        AnsiConsole.MarkupLine("[bold blue]Manage profiles:[/]\n");

        var table = new Table();
        table.AddColumn("#");
        table.AddColumn("");
        table.AddColumn("Name");
        table.AddColumn("Type");
        table.AddColumn("Token Limits");

        for (int i = 0; i < profileList.Count; i++)
        {
            var profile = profileList[i];
            var marker = profile.Name == lastUsed ? "[green]*[/]" : " ";
            var tokenInfo = FormatProfileTokenInfo(profile);
            table.AddRow($"[dim]{i + 1}[/]", marker, $"[cyan]{profile.Name}[/]", profile.Type, tokenInfo);
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("");

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Action:")
                .AddChoices(new[] { "Use profile", "Remove profile(s)", "Add profile", "Import from Foundry", "MCP compat servers", "Exit" }));

        if (action == "Use profile")
        {
            var input = AnsiConsole.Ask<string>("[yellow]Profile #[/]: ").Trim();
            if (int.TryParse(input, out var selection) && selection > 0 && selection <= profileList.Count)
            {
                var selectedProfile = profileList[selection - 1];
                return UseCommand(new[] { selectedProfile.Name });
            }

            AnsiConsole.MarkupLine("[red]Invalid selection.[/]");
            return 1;
        }

        if (action == "Remove profile(s)")
        {
            var removable = profileList
                .Where(p => !p.Name.Equals("default", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (removable.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No removable profiles found.[/]");
                return 0;
            }

            var removePrompt = new MultiSelectionPrompt<string>()
                .Title("Select profile(s) to [bold red]remove[/]:")
                .NotRequired()
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to confirm)[/]");

            foreach (var p in removable)
            {
                removePrompt.AddChoice(p.Name);
            }

            var targets = AnsiConsole.Prompt(removePrompt);
            if (targets.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No profiles selected.[/]");
                return 0;
            }

            var result = ConfigManager.RemoveProfiles(targets);
            if (!result.Ok)
            {
                AnsiConsole.MarkupLine("[red]Failed to remove profiles.[/]");
                return 1;
            }

            AnsiConsole.MarkupLine($"[green]Removed {result.Removed} profile(s).[/]");
            return 0;
        }

        if (action == "Add profile")
        {
            return AddCommand();
        }

        if (action == "Import from Foundry")
        {
            return ImportFoundryCommand(Array.Empty<string>()).GetAwaiter().GetResult();
        }

        if (action == "MCP compat servers")
        {
            var compatibleProfiles = profileList.Where(IsAzureByokLikeProfile).ToList();
            if (compatibleProfiles.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No Azure BYOK/proxy profiles found for MCP compatibility settings.[/]");
                return 0;
            }

            var selectionInput = AnsiConsole.Ask<string>("[yellow]Profile # (from the table above)[/]: ").Trim();
            if (!int.TryParse(selectionInput, out var profileSelection)
                || profileSelection <= 0
                || profileSelection > profileList.Count)
            {
                AnsiConsole.MarkupLine("[red]Invalid selection.[/]");
                return 1;
            }

            var selectedProfile = profileList[profileSelection - 1];
            if (!IsAzureByokLikeProfile(selectedProfile))
            {
                AnsiConsole.MarkupLine("[red]Selected profile is not an Azure BYOK/proxy profile.[/]");
                return 1;
            }

            var mode = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("MCP action:")
                    .AddChoices(new[] { "Set (interactive)", "Reset", "All", "None" }));

            var actionName = mode switch
            {
                "Reset" => "reset",
                "All" => "all",
                "None" => "none",
                _ => "set"
            };

            return ConfigureMcpCompatForProfile(selectedProfile.Name, actionName);
        }

        return 0;
    }

    static bool IsAzureByokLikeProfile(Profile profile)
    {
        return (profile.Type == "byok" || profile.Type == "proxy") && IsAzureProfile(profile);
    }

    static int ConfigureMcpCompatForProfile(string profileName, string action)
    {
        var profile = ConfigManager.GetProfile(profileName);
        if (profile == null)
        {
            AnsiConsole.MarkupLine($"[red]Error: Profile '{EscapeMarkup(profileName)}' not found[/]");
            return 1;
        }

        if (!IsAzureByokLikeProfile(profile))
        {
            AnsiConsole.MarkupLine("[red]MCP compatibility server selection applies only to Azure BYOK/proxy profiles.[/]");
            return 1;
        }

        var mode = (action ?? "set").Trim().ToLowerInvariant();

        if (mode == "reset")
        {
            profile.McpCompatServers = null;
            ConfigManager.AddProfile(profile);
            AnsiConsole.MarkupLine($"[green]Reset MCP compatibility server selection for profile:[/] {EscapeMarkup(profile.Name)}");
            AnsiConsole.MarkupLine("[dim]The next interactive use will prompt for MCP server selection again.[/]");
            return 0;
        }

        if (mode == "all")
        {
            var discovered = DiscoverMcpServers();
            profile.McpCompatServers = discovered.Count > 0
                ? discovered
                : new List<string>(DefaultMcpCompatServers);
            ConfigManager.AddProfile(profile);
            AnsiConsole.MarkupLine($"[green]Set MCP compatibility servers to all candidates for profile:[/] {EscapeMarkup(profile.Name)}");
            return 0;
        }

        if (mode == "none")
        {
            profile.McpCompatServers = [];
            ConfigManager.AddProfile(profile);
            AnsiConsole.MarkupLine($"[green]Set MCP compatibility servers to none for profile:[/] {EscapeMarkup(profile.Name)}");
            return 0;
        }

        profile.McpCompatServers = PromptMcpCompatServers(profile.McpCompatServers);
        ConfigManager.AddProfile(profile);
        AnsiConsole.MarkupLine($"[green]Saved MCP compatibility server selection for profile:[/] {EscapeMarkup(profile.Name)}");
        return 0;
    }

    static int McpCompatCommand(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: Profile name required[/]");
            AnsiConsole.MarkupLine("Usage: copilot-byok-model-switcher mcp-compat <profile> [--action set|reset|all|none]");
            return 1;
        }

        var profileName = args[0];
        var action = "set";

        for (var i = 1; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--action", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                action = args[++i];
                continue;
            }

            if (arg.StartsWith("--action=", StringComparison.OrdinalIgnoreCase))
            {
                action = arg[("--action=".Length)..];
                continue;
            }
        }

        var normalized = action.Trim().ToLowerInvariant();
        if (normalized != "set" && normalized != "reset" && normalized != "all" && normalized != "none")
        {
            AnsiConsole.MarkupLine("[red]Error: --action must be one of set, reset, all, none[/]");
            return 1;
        }

        return ConfigureMcpCompatForProfile(profileName, normalized);
    }

    static int RemoveCommand(string[] args)
    {
        List<string> targets;

        if (args.Length > 0)
        {
            targets = args.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
        }
        else
        {
            var removableProfiles = ConfigManager.ListProfiles()
                .Where(p => !p.Name.Equals("default", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (removableProfiles.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No removable profiles found.[/]");
                return 0;
            }

            var prompt = new MultiSelectionPrompt<string>()
                .Title("Select profile(s) to [bold red]remove[/]:")
                .NotRequired()
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to confirm)[/]");

            foreach (var profile in removableProfiles)
            {
                prompt.AddChoice(profile.Name);
            }

            targets = AnsiConsole.Prompt(prompt);
        }

        if (targets.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No profiles selected.[/]");
            return 0;
        }

        var result = ConfigManager.RemoveProfiles(targets);
        if (!result.Ok)
        {
            AnsiConsole.MarkupLine("[red]Failed to remove profiles.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Removed {result.Removed} profile(s).[/]");
        return 0;
    }

    static int UseCommand(string[] args)
    {
        if (args.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Error: Profile name required[/]");
            AnsiConsole.MarkupLine("Usage: copilot-byok-model-switcher use <profile> [args...]");
            return 1;
        }

        var profileName = args[0];
        var copilotArgs = args.Skip(1).ToArray();

        return ExecuteWithProfile(profileName, copilotArgs).GetAwaiter().GetResult();
    }

    static int LastCommand(string[] args)
    {
        var lastUsed = ConfigManager.GetLastUsed();

        if (string.IsNullOrEmpty(lastUsed))
        {
            AnsiConsole.MarkupLine("[red]Error: No profile has been used yet[/]");
            return 1;
        }

        return ExecuteWithProfile(lastUsed, args).GetAwaiter().GetResult();
    }

    static int DefaultCommand(string[] args)
    {
        return ExecuteWithProfile("default", args).GetAwaiter().GetResult();
    }

    static async Task<int> ExecuteWithProfile(string profileName, string[] copilotArgs)
    {
        var profile = ConfigManager.GetProfile(profileName);

        if (profile == null)
        {
            AnsiConsole.MarkupLine($"[red]Error: Profile '{Markup.Escape(profileName)}' not found[/]");
            AnsiConsole.MarkupLine("Use 'copilot-byok-model-switcher list' to see available profiles.");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Using profile:[/] {Markup.Escape(profile.Name)} ([dim]{Markup.Escape(profile.Type)}[/])");
        AnsiConsole.MarkupLine($"[dim]Token limits: {EscapeMarkup(FormatProfileTokenInfo(profile))}[/]");

        AuthEnvironmentResult envInfo;
        try
        {
            envInfo = await SetEnvironmentForProfile(profile);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error setting auth environment: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[dim]For Azure CLI token auth, ensure az is installed and you are logged in: az login[/]");
            return 1;
        }

        ConfigManager.SetLastUsed(profileName);

        var userRequestedInteractive = copilotArgs.Length == 0;

        // For Azure BYOK profiles in interactive mode, prompt for MCP servers to disable on first use.
        var needsCompat = !(Environment.GetEnvironmentVariable("CBMS_DISABLE_MCP_COMPAT")
            ?? Environment.GetEnvironmentVariable("COPILOTX_DISABLE_MCP_COMPAT")
            ?? string.Empty)
            .Trim().Equals("off", StringComparison.OrdinalIgnoreCase)
            && (profile.Type == "byok" || profile.Type == "proxy")
            && IsAzureProfile(profile);
        var hasManualMcpControls = copilotArgs.Any(a =>
            a.Equals("--disable-mcp-server", StringComparison.OrdinalIgnoreCase)
            || a.Equals("--disable-builtin-mcps", StringComparison.OrdinalIgnoreCase)
            || a.Equals("--available-tools", StringComparison.OrdinalIgnoreCase));

        if (needsCompat && !hasManualMcpControls && userRequestedInteractive
            && profile.McpCompatServers == null && !Console.IsInputRedirected)
        {
            profile.McpCompatServers = PromptMcpCompatServers(null);
            ConfigManager.AddProfile(profile);
        }

        var effectiveCopilotArgs = BuildCopilotArgs(profile, copilotArgs);

        try
        {
            var result = await RunCopilot(effectiveCopilotArgs, userRequestedInteractive);

            if (result.ExitCode != 0 && envInfo.UsedAzureCliToken && IsTokenFailure(result.Output))
            {
                AnsiConsole.MarkupLine("[yellow]Detected token-related auth failure. Refreshing Azure CLI token and retrying once...[/]");
                var refreshedToken = await GetAzureCliToken(profile);
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_API_KEY", null);
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_BEARER_TOKEN", refreshedToken);
                result = await RunCopilot(effectiveCopilotArgs, userRequestedInteractive);
            }

            return result.ExitCode;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error executing gh copilot: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[dim]Make sure GitHub Copilot CLI is installed: gh extension install github/gh-copilot[/]");
            return 1;
        }
    }

    static string[] BuildCopilotArgs(Profile profile, string[] copilotArgs)
    {
        var disableCompat = (Environment.GetEnvironmentVariable("CBMS_DISABLE_MCP_COMPAT")
            ?? Environment.GetEnvironmentVariable("COPILOTX_DISABLE_MCP_COMPAT")
            ?? string.Empty)
            .Trim()
            .Equals("off", StringComparison.OrdinalIgnoreCase);

        var args = new List<string>(copilotArgs);

        var hasPromptMode = args.Any(a =>
            a.Equals("-p", StringComparison.OrdinalIgnoreCase)
            || a.Equals("--prompt", StringComparison.OrdinalIgnoreCase));

        var hasPermissionControls = args.Any(a =>
            a.Equals("--allow-all-tools", StringComparison.OrdinalIgnoreCase)
            || a.Equals("--allow-all", StringComparison.OrdinalIgnoreCase)
            || a.Equals("--yolo", StringComparison.OrdinalIgnoreCase)
            || a.Equals("--allow-tool", StringComparison.OrdinalIgnoreCase));

        // In non-interactive prompt mode, allow tools automatically so CLI doesn't fail
        // with "could not request permission from user".
        if (hasPromptMode && !hasPermissionControls)
        {
            args.Add("--allow-all-tools");
        }

        // Azure BYOK providers can exceed tool-count limits when many MCP servers are present.
        if (!disableCompat && (profile.Type == "byok" || profile.Type == "proxy") && IsAzureProfile(profile))
        {
            var hasManualMcpControls = args.Any(a =>
                a.Equals("--disable-mcp-server", StringComparison.OrdinalIgnoreCase)
                || a.Equals("--disable-builtin-mcps", StringComparison.OrdinalIgnoreCase)
                || a.Equals("--available-tools", StringComparison.OrdinalIgnoreCase));

            if (!hasManualMcpControls)
            {
                var serversToDisable = profile.McpCompatServers ?? new List<string>(DefaultMcpCompatServers);
                var mcpArgs = serversToDisable.SelectMany(s => new[] { "--disable-mcp-server", s }).ToList();
                var prefix = new List<string> { "--disable-builtin-mcps" };
                prefix.AddRange(mcpArgs);
                AnsiConsole.MarkupLine("[dim]Applying Azure BYOK MCP compatibility mode to avoid provider tool-limit errors.[/]");
                return prefix.Concat(args).ToArray();
            }
        }

        return args.ToArray();
    }

    static List<string> DiscoverMcpServers()
    {
        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Strategy 1: Project-level then user-level settings.json / mcp.json
        foreach (var settingsPath in GetMcpConfigPaths())
        {
            try
            {
                if (!File.Exists(settingsPath)) continue;
                using var stream = File.OpenRead(settingsPath);
                var doc = JsonDocument.Parse(stream);
                // settings.json: { "mcp": { "servers": { ... } } }
                // mcp.json:      { "servers": { ... } }
                JsonElement servers = default;
                if (doc.RootElement.TryGetProperty("mcp", out var mcp)
                    && mcp.TryGetProperty("servers", out servers))
                { /* servers already set */ }
                else
                {
                    doc.RootElement.TryGetProperty("servers", out servers);
                }
                if (servers.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in servers.EnumerateObject())
                        discovered.Add(prop.Name);
                }
            }
            catch { /* ignore read/parse errors */ }
        }

        // Strategy 2: gh config list — look for copilot.mcp* entries
        try
        {
            var psi = new ProcessStartInfo("gh", "config list")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(5000);
                foreach (var line in output.Split('\n'))
                {
                    var m = Regex.Match(line.Trim(),
                        @"^copilot\.mcp[_-]?servers?\.([^.\s]+)",
                        RegexOptions.IgnoreCase);
                    if (m.Success) discovered.Add(m.Groups[1].Value);
                }
            }
        }
        catch { /* gh not found or error */ }

        return [.. discovered];
    }

    static IEnumerable<string> GetMcpConfigPaths()
    {
        // 1) Project/workspace-level config first (walk up from current directory)
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static bool AddSeen(HashSet<string> set, string p) => set.Add(Path.GetFullPath(p));

        var dir = Environment.CurrentDirectory;
        while (true)
        {
            var p1 = Path.Combine(dir, "mcp.json");
            if (AddSeen(seen, p1)) yield return p1;

            var p2 = Path.Combine(dir, ".vscode", "mcp.json");
            if (AddSeen(seen, p2)) yield return p2;

            var p3 = Path.Combine(dir, ".vscode", "settings.json");
            if (AddSeen(seen, p3)) yield return p3;

            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        // 2) User-level config next
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var p1 = Path.Combine(appData, "Code", "User", "settings.json");
            if (AddSeen(seen, p1)) yield return p1;
            var p2 = Path.Combine(appData, "Code", "User", "mcp.json");
            if (AddSeen(seen, p2)) yield return p2;
        }
        else if (OperatingSystem.IsMacOS())
        {
            var base2 = Path.Combine(home, "Library", "Application Support", "Code", "User");
            var p1 = Path.Combine(base2, "settings.json");
            if (AddSeen(seen, p1)) yield return p1;
            var p2 = Path.Combine(base2, "mcp.json");
            if (AddSeen(seen, p2)) yield return p2;
        }
        else
        {
            var base2 = Path.Combine(home, ".config", "Code", "User");
            var p1 = Path.Combine(base2, "settings.json");
            if (AddSeen(seen, p1)) yield return p1;
            var p2 = Path.Combine(base2, "mcp.json");
            if (AddSeen(seen, p2)) yield return p2;
        }
    }

    static List<string> PromptMcpCompatServers(List<string>? previousSelection)
    {
        var discovered = DiscoverMcpServers();
        var candidates = discovered.Count > 0 ? discovered : new List<string>(DefaultMcpCompatServers);
        var isDiscovered = discovered.Count > 0;

        // Default selection: all candidates (disable everything for maximum compat)
        var previous = previousSelection ?? candidates;

        if (isDiscovered)
            AnsiConsole.MarkupLine($"[dim]Discovered {candidates.Count} configured MCP server(s).[/]");
        else
            AnsiConsole.MarkupLine("[dim]Could not auto-discover MCP servers. Using known candidates as fallback.[/]");

        var prompt = new MultiSelectionPrompt<string>()
            .Title("Select MCP servers to [bold]disable[/] for Azure BYOK compat mode:")
            .NotRequired()
            .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to confirm)[/]");

        foreach (var server in candidates)
        {
            var choice = prompt.AddChoice(server);
            if (previous.Contains(server, StringComparer.OrdinalIgnoreCase))
            {
                choice.Select();
            }
        }

        return AnsiConsole.Prompt(prompt);
    }

    static bool IsAzureProfile(Profile profile)
    {
        var baseUrl = profile.BaseUrl?.ToLowerInvariant() ?? string.Empty;
        var providerType = profile.ProviderType?.ToLowerInvariant() ?? string.Empty;
        return baseUrl.Contains(".openai.azure.com") || providerType == "azure";
    }

    static string GetAzureDeploymentFromBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return string.Empty;
        var match = Regex.Match(baseUrl, @"/openai/deployments/([^/?#]+)", RegexOptions.IgnoreCase);
        if (!match.Success) return string.Empty;
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    static bool ShouldUseAzureCliToken(Profile profile, bool hasApiKey)
    {
        var mode = (profile.AzureCliToken ?? "auto").ToLowerInvariant();

        if (mode == "on")
        {
            return true;
        }

        if (mode == "off")
        {
            return false;
        }

        return !hasApiKey && IsAzureProfile(profile);
    }

    static async Task<string> GetAzureCliToken(Profile profile)
    {
        var scope = string.IsNullOrWhiteSpace(profile.TokenScope)
            ? "https://cognitiveservices.azure.com/.default"
            : profile.TokenScope;

        ProcessStartInfo startInfo;

        if (OperatingSystem.IsWindows())
        {
            var commandLine = string.Join(" ", new[] { "az", "account", "get-access-token", "--scope", QuoteForCmd(scope), "--query", "accessToken", "-o", "tsv" });
            startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/d /s /c \"{commandLine}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }
        else
        {
            startInfo = new ProcessStartInfo
            {
                FileName = GetAzureCliCommand(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            startInfo.ArgumentList.Add("account");
            startInfo.ArgumentList.Add("get-access-token");
            startInfo.ArgumentList.Add("--scope");
            startInfo.ArgumentList.Add(scope);
            startInfo.ArgumentList.Add("--query");
            startInfo.ArgumentList.Add("accessToken");
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add("tsv");
        }

        var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start az CLI process.");
        }

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"az account get-access-token failed (exit {process.ExitCode}): {stderr.Trim()}");
        }

        var token = stdout.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("az CLI returned an empty access token.");
        }

        return token;
    }

    internal static void SetProviderTokenLimitEnvironment(Profile profile)
    {
        if (profile.MaxOutputTokens.HasValue)
        {
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_MAX_OUTPUT_TOKENS", profile.MaxOutputTokens.Value.ToString());
        }
        else
        {
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_MAX_OUTPUT_TOKENS", null);
        }

        if (profile.MaxPromptTokens.HasValue)
        {
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_MAX_PROMPT_TOKENS", profile.MaxPromptTokens.Value.ToString());
        }
        else
        {
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_MAX_PROMPT_TOKENS", null);
        }
    }

    static int? AskOptionalInt(string prompt)
    {
        while (true)
        {
            var input = AnsiConsole.Ask<string>(prompt, string.Empty).Trim();
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            if (int.TryParse(input, out var value) && value > 0)
            {
                return value;
            }

            AnsiConsole.MarkupLine("[red]Please enter a positive integer or leave it blank.[/]");
        }
    }

    static int? AskOptionalIntWithDefault(string prompt, int? defaultValue)
    {
        while (true)
        {
            var defaultLabel = defaultValue.HasValue ? defaultValue.Value.ToString() : "not set";
            var input = AnsiConsole.Ask<string>($"{prompt} [dim](Enter for default: {defaultLabel})[/]:", string.Empty).Trim();

            if (string.IsNullOrEmpty(input))
            {
                return defaultValue;
            }

            if (int.TryParse(input, out var value) && value > 0)
            {
                return value;
            }

            AnsiConsole.MarkupLine("[red]Please enter a positive integer or leave it blank.[/]");
        }
    }

    static string FormatOptionalInt(int? value)
    {
        return value.HasValue ? value.Value.ToString() : "not set";
    }

    internal static string FormatProfileTokenInfo(Profile profile)
    {
        if (!profile.MaxOutputTokens.HasValue && !profile.MaxPromptTokens.HasValue)
        {
            return "not set";
        }

        return $"output={FormatOptionalInt(profile.MaxOutputTokens)}, prompt={FormatOptionalInt(profile.MaxPromptTokens)}";
    }

    static int? ParseOptionalPositiveIntOption(string? rawValue, string optionName)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        if (int.TryParse(rawValue, out var value) && value > 0)
        {
            return value;
        }

        throw new InvalidOperationException($"Option --{optionName} must be a positive integer.");
    }

    static async Task<AuthEnvironmentResult> SetEnvironmentForProfile(Profile profile)
    {
        if (profile.Type == "copilot")
        {
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_BASE_URL", null);
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_API_KEY", null);
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_BEARER_TOKEN", null);
            Environment.SetEnvironmentVariable("COPILOT_MODEL", null);
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_TYPE", null);
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_MAX_OUTPUT_TOKENS", null);
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_MAX_PROMPT_TOKENS", null);
            return new AuthEnvironmentResult { UsedAzureCliToken = false };
        }
        else if (profile.Type == "byok" || profile.Type == "proxy")
        {
            if (!string.IsNullOrEmpty(profile.BaseUrl))
            {
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_BASE_URL", profile.BaseUrl);
            }

            if (!string.IsNullOrEmpty(profile.Model))
            {
                var modelForProvider = profile.Model;
                if (IsAzureProfile(profile))
                {
                    var deploymentName = GetAzureDeploymentFromBaseUrl(profile.BaseUrl);
                    if (!string.IsNullOrWhiteSpace(deploymentName)
                        && !string.Equals(modelForProvider, deploymentName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Azure BYOK providers expect the deployment identifier as COPILOT_MODEL.
                        modelForProvider = deploymentName;
                        AnsiConsole.MarkupLine($"[dim]Using Azure deployment name '{EscapeMarkup(deploymentName)}' as model for provider compatibility.[/]");
                    }
                }

                Environment.SetEnvironmentVariable("COPILOT_MODEL", modelForProvider);
            }

            string? resolvedApiKey = null;

            if (!string.IsNullOrEmpty(profile.ApiKeyEnv))
            {
                var apiKey = Environment.GetEnvironmentVariable(profile.ApiKeyEnv);
                if (!string.IsNullOrEmpty(apiKey))
                {
                    resolvedApiKey = apiKey;
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning: Environment variable {profile.ApiKeyEnv} is not set[/]");
                }
            }
            else if (!string.IsNullOrEmpty(profile.ApiKey))
            {
                resolvedApiKey = profile.ApiKey;
            }

            var useAzureCliToken = ShouldUseAzureCliToken(profile, !string.IsNullOrEmpty(resolvedApiKey));
            if (useAzureCliToken)
            {
                var token = await GetAzureCliToken(profile);
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_API_KEY", null);
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_BEARER_TOKEN", token);
            }
            else if (!string.IsNullOrEmpty(resolvedApiKey))
            {
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_API_KEY", resolvedApiKey);
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_BEARER_TOKEN", null);
            }
            else
            {
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_API_KEY", null);
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_BEARER_TOKEN", null);
            }

            if (!string.IsNullOrEmpty(profile.ProviderType))
            {
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_TYPE", profile.ProviderType);
            }

            SetProviderTokenLimitEnvironment(profile);

            return new AuthEnvironmentResult { UsedAzureCliToken = useAzureCliToken };
        }

        return new AuthEnvironmentResult { UsedAzureCliToken = false };
    }

    static bool IsTokenFailure(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        var lower = output.ToLowerInvariant();
        return lower.Contains("401")
            || lower.Contains("unauthorized")
            || lower.Contains("forbidden")
            || lower.Contains("invalid token")
            || lower.Contains("token expired")
            || lower.Contains("expired token")
            || lower.Contains("authentication failed")
            || lower.Contains("permission denied");
    }

    static async Task<ProcessRunResult> RunCopilot(string[] copilotArgs, bool interactiveMode)
    {
        if (interactiveMode)
        {
            AnsiConsole.MarkupLine("[dim]Launching gh copilot in interactive mode. Type your question below:[/]");
            AnsiConsole.MarkupLine("[dim]If prompted to trust this folder, choose option 2 once to remember it.[/]");
            AnsiConsole.MarkupLine("");

            // Interactive gh copilot expects a real terminal (TTY). Avoid redirected pipes here.
            var interactiveStartInfo = new ProcessStartInfo
            {
                FileName = "gh",
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            interactiveStartInfo.ArgumentList.Add("copilot");
            if (copilotArgs.Length > 0)
            {
                interactiveStartInfo.ArgumentList.Add("--");
                foreach (var arg in copilotArgs)
                {
                    interactiveStartInfo.ArgumentList.Add(arg);
                }
            }

            var interactiveProcess = Process.Start(interactiveStartInfo);
            if (interactiveProcess == null)
            {
                throw new InvalidOperationException("Failed to start gh copilot.");
            }

            await interactiveProcess.WaitForExitAsync();

            return new ProcessRunResult
            {
                ExitCode = interactiveProcess.ExitCode,
                Output = string.Empty
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "gh",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("copilot");
        if (copilotArgs.Length > 0)
        {
            startInfo.ArgumentList.Add("--");
            foreach (var arg in copilotArgs)
            {
                startInfo.ArgumentList.Add(arg);
            }
        }

        var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start gh copilot.");
        }

        _ = Task.Run(async () =>
        {
            await Console.OpenStandardInput().CopyToAsync(process.StandardInput.BaseStream);
            process.StandardInput.Close();
        });

        var outputBuilder = new StringBuilder();

        var stdoutTask = Task.Run(async () =>
        {
            while (true)
            {
                var line = await process.StandardOutput.ReadLineAsync();
                if (line == null)
                {
                    break;
                }

                outputBuilder.AppendLine(line);
                Console.Out.WriteLine(line);
            }
        });

        var stderrTask = Task.Run(async () =>
        {
            while (true)
            {
                var line = await process.StandardError.ReadLineAsync();
                if (line == null)
                {
                    break;
                }

                outputBuilder.AppendLine(line);
                Console.Error.WriteLine(line);
            }
        });

        await Task.WhenAll(stdoutTask, stderrTask, process.WaitForExitAsync());

        return new ProcessRunResult
        {
            ExitCode = process.ExitCode,
            Output = outputBuilder.ToString()
        };
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

            profile.AzureCliToken = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Azure CLI token [cyan]mode[/]:")
                    .AddChoices(new[] { "auto", "on", "off" }));

            if (profile.AzureCliToken == "auto" || profile.AzureCliToken == "on")
            {
                profile.TokenScope = AnsiConsole.Ask<string>("Azure token [cyan]scope[/] (optional):", string.Empty);
            }

            profile.MaxOutputTokens = AskOptionalInt("Max [cyan]output tokens[/] (optional):");
            profile.MaxPromptTokens = AskOptionalInt("Max [cyan]prompt tokens[/] (optional):");
        }

        var upsert = ConfigManager.UpsertProfile(profile);
        if (!upsert.Ok)
        {
            AnsiConsole.MarkupLine("[red]Error: Failed to add profile[/]");
            return 1;
        }

        if (upsert.Action == "added")
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Profile '{EscapeMarkup(upsert.Name)}' added successfully!");
        }
        else if (upsert.Action == "updated-equivalent")
        {
            AnsiConsole.MarkupLine($"[yellow]Equivalent profile already existed; updated '{EscapeMarkup(upsert.Name)}' instead of creating a duplicate.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Profile '{EscapeMarkup(upsert.Name)}' updated successfully!");
        }

        return 0;
    }

    static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = arg.Substring(2);
            var value = "true";

            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[i + 1];
                i += 1;
            }

            options[key] = value;
        }

        return options;
    }

    static async Task<JsonDocument> RunAzJson(params string[] azArgs)
    {
        ProcessStartInfo startInfo;

        if (OperatingSystem.IsWindows())
        {
            var commandLine = string.Join(" ", new[] { "az" }.Concat(azArgs.Select(QuoteForCmd)));
            startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/d /s /c \"{commandLine}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }
        else
        {
            startInfo = new ProcessStartInfo
            {
                FileName = GetAzureCliCommand(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (var arg in azArgs)
            {
                startInfo.ArgumentList.Add(arg);
            }
        }

        var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start az CLI process.");
        }

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"az {string.Join(" ", azArgs)} failed (exit {process.ExitCode}): {stderr.Trim()}");
        }

        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(stdout) ? "[]" : stdout);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse az output: {ex.Message}");
        }
    }

    static async Task<List<FoundryAccount>> ListFoundryAccounts(string subscription)
    {
        JsonDocument doc;
        if (string.IsNullOrWhiteSpace(subscription))
        {
            doc = await RunAzJson(
                "cognitiveservices", "account", "list",
                "--query", "[].{name:name,resourceGroup:resourceGroup,kind:kind,endpoint:properties.endpoint}",
                "-o", "json");
        }
        else
        {
            doc = await RunAzJson(
                "cognitiveservices", "account", "list",
                "--subscription", subscription,
                "--query", "[].{name:name,resourceGroup:resourceGroup,kind:kind,endpoint:properties.endpoint}",
                "-o", "json");
        }

        using (doc)
        {
            var result = new List<FoundryAccount>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty;
                var resourceGroup = item.TryGetProperty("resourceGroup", out var rgProp) ? rgProp.GetString() ?? string.Empty : string.Empty;
                var kind = item.TryGetProperty("kind", out var kindProp) ? kindProp.GetString() ?? string.Empty : string.Empty;

                var endpoint = item.TryGetProperty("endpoint", out var endpointProp) ? endpointProp.GetString() ?? string.Empty : string.Empty;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(resourceGroup))
                {
                    continue;
                }

                if (!FoundryImportHelpers.IsApplicableAccount(item))
                {
                    continue;
                }

                result.Add(new FoundryAccount
                {
                    Name = name,
                    ResourceGroup = resourceGroup,
                    Endpoint = endpoint
                });
            }

            return result;
        }
    }

    static async Task<List<FoundryDeployment>> ListAccountDeployments(string accountName, string resourceGroup, string subscription)
    {
        JsonDocument doc;
        if (string.IsNullOrWhiteSpace(subscription))
        {
            doc = await RunAzJson(
                "cognitiveservices", "account", "deployment", "list",
                "--name", accountName,
                "--resource-group", resourceGroup,
                "-o", "json");
        }
        else
        {
            doc = await RunAzJson(
                "cognitiveservices", "account", "deployment", "list",
                "--name", accountName,
                "--resource-group", resourceGroup,
                "--subscription", subscription,
                "-o", "json");
        }

        using (doc)
        {
            var result = new List<FoundryDeployment>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!FoundryImportHelpers.IsChatCapableDeployment(item))
                {
                    continue;
                }

                var deployment = FoundryImportHelpers.MapDeployment(item);
                if (!string.IsNullOrWhiteSpace(deployment.DeploymentName))
                {
                    result.Add(deployment);
                }
            }

            return result;
        }
    }

    static async Task<int> ImportFoundryCommand(string[] args)
    {
        if (args.Any(arg => arg is "--help" or "-h"))
        {
            AnsiConsole.MarkupLine("[bold]import-foundry[/]");
            AnsiConsole.MarkupLine("Discover Foundry/Azure OpenAI deployments and add them as profiles.\n");
            AnsiConsole.MarkupLine("[cyan]Options:[/]");
            AnsiConsole.MarkupLine("  --account <name>           Limit import to one account");
            AnsiConsole.MarkupLine("  --resource-group <name>    Resource group for --account");
            AnsiConsole.MarkupLine("  --subscription <id|name>   Limit discovery to one subscription");
            AnsiConsole.MarkupLine("  --mode each|all            Prompt per deployment or add all");
            AnsiConsole.MarkupLine("  --all                      Add all discovered deployments without prompts\n");
            AnsiConsole.MarkupLine("  --max-output-tokens <n>    Set max output tokens on imported profiles");
            AnsiConsole.MarkupLine("  --max-prompt-tokens <n>    Set max prompt tokens on imported profiles\n");
            AnsiConsole.MarkupLine("[cyan]Examples:[/]");
            AnsiConsole.MarkupLine("  copilot-byok-model-switcher import-foundry --mode each");
            AnsiConsole.MarkupLine("  copilot-byok-model-switcher import-foundry --all");
            AnsiConsole.MarkupLine("  copilot-byok-model-switcher import-foundry --all --max-output-tokens 4096 --max-prompt-tokens 64000");
            AnsiConsole.MarkupLine("  copilot-byok-model-switcher import-foundry --account myfoundry --resource-group my-rg --all");
            return 0;
        }

        var options = ParseOptions(args);
        options.TryGetValue("account", out var account);
        options.TryGetValue("resource-group", out var resourceGroup);
        options.TryGetValue("subscription", out var subscription);
        options.TryGetValue("mode", out var mode);
        options.TryGetValue("max-output-tokens", out var maxOutputTokensRaw);
        options.TryGetValue("max-prompt-tokens", out var maxPromptTokensRaw);

        var hasExplicitMaxOutputTokens = options.ContainsKey("max-output-tokens");
        var hasExplicitMaxPromptTokens = options.ContainsKey("max-prompt-tokens");

        var maxOutputTokens = ParseOptionalPositiveIntOption(maxOutputTokensRaw, "max-output-tokens");
        var maxPromptTokens = ParseOptionalPositiveIntOption(maxPromptTokensRaw, "max-prompt-tokens");

        var addAll = options.ContainsKey("all");
        var effectiveMode = addAll ? "all" : (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (effectiveMode != "all" && effectiveMode != "each")
        {
            effectiveMode = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Import mode:")
                    .AddChoices(new[] { "each", "all" }));
        }

        var shouldPromptForTokenLimits =
            !options.ContainsKey("mode")
            && !options.ContainsKey("all")
            && !options.ContainsKey("max-output-tokens")
            && !options.ContainsKey("max-prompt-tokens");

        if (shouldPromptForTokenLimits)
        {
            maxOutputTokens = AskOptionalInt("Default fallback max [cyan]output tokens[/] when deployment metadata has no suggestion (optional):");
            maxPromptTokens = AskOptionalInt("Default fallback max [cyan]prompt tokens[/] when deployment metadata has no suggestion (optional):");
        }

        try
        {
            List<FoundryAccount> accounts;
            if (!string.IsNullOrWhiteSpace(account) && !string.IsNullOrWhiteSpace(resourceGroup))
            {
                accounts = new List<FoundryAccount>
                {
                    new FoundryAccount
                    {
                        Name = account,
                        ResourceGroup = resourceGroup,
                        Endpoint = $"https://{account}.openai.azure.com"
                    }
                };
            }
            else
            {
                accounts = await ListFoundryAccounts(subscription ?? string.Empty);
            }

            if (accounts.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No OpenAI/Foundry accounts found.[/]");
                return 0;
            }

            var imported = 0;
            var scanned = 0;
            var existingProfiles = ConfigManager.ListProfiles();
            var existingNames = new HashSet<string>(existingProfiles.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
            var existingProfilesByName = existingProfiles
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var outputLabel = hasExplicitMaxOutputTokens
                ? $"{FormatOptionalInt(maxOutputTokens)} (explicit override)"
                : (maxOutputTokens.HasValue
                    ? $"metadata first, fallback={maxOutputTokens.Value}"
                    : "metadata first, fallback=not set");
            var promptLabel = hasExplicitMaxPromptTokens
                ? $"{FormatOptionalInt(maxPromptTokens)} (explicit override)"
                : (maxPromptTokens.HasValue
                    ? $"metadata first, fallback={maxPromptTokens.Value}"
                    : "metadata first, fallback=not set");
            AnsiConsole.MarkupLine($"[dim]Import token limits: output={EscapeMarkup(outputLabel)}, prompt={EscapeMarkup(promptLabel)}[/]");

            foreach (var foundryAccount in accounts)
            {
                List<FoundryDeployment> deployments;
                try
                {
                    deployments = await ListAccountDeployments(
                        foundryAccount.Name,
                        foundryAccount.ResourceGroup,
                        subscription ?? string.Empty);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]Skipping {EscapeMarkup(foundryAccount.Name)}: {EscapeMarkup(ex.Message)}[/]");
                    continue;
                }

                if (deployments.Count == 0)
                {
                    continue;
                }

                AnsiConsole.MarkupLine($"\n[bold]Account:[/] {EscapeMarkup(foundryAccount.Name)} ([dim]{EscapeMarkup(foundryAccount.ResourceGroup)}[/])");
                var endpoint = (string.IsNullOrWhiteSpace(foundryAccount.Endpoint)
                    ? $"https://{foundryAccount.Name}.openai.azure.com"
                    : foundryAccount.Endpoint).TrimEnd('/');

                foreach (var deployment in deployments)
                {
                    scanned += 1;
                    var shouldAdd = effectiveMode == "all";
                    var modelLabel = string.IsNullOrWhiteSpace(deployment.ModelVersion)
                        ? deployment.ModelName
                        : $"{deployment.ModelName}:{deployment.ModelVersion}";

                    if (!shouldAdd)
                    {
                        shouldAdd = AnsiConsole.Confirm(
                            $"Add deployment '{deployment.DeploymentName}' ({modelLabel}) as profile?",
                            false);
                    }

                    if (!shouldAdd)
                    {
                        continue;
                    }

                    var proposedOutputTokens = hasExplicitMaxOutputTokens
                        ? maxOutputTokens
                        : (deployment.SuggestedMaxOutputTokens ?? maxOutputTokens);
                    var proposedPromptTokens = hasExplicitMaxPromptTokens
                        ? maxPromptTokens
                        : (deployment.SuggestedMaxPromptTokens ?? maxPromptTokens);

                    var outputSource = hasExplicitMaxOutputTokens
                        ? "explicit"
                        : (deployment.SuggestedMaxOutputTokens.HasValue
                            ? deployment.SuggestedMaxOutputTokensSource
                            : (maxOutputTokens.HasValue ? "fallback" : "not-set"));
                    var promptSource = hasExplicitMaxPromptTokens
                        ? "explicit"
                        : (deployment.SuggestedMaxPromptTokens.HasValue
                            ? deployment.SuggestedMaxPromptTokensSource
                            : (maxPromptTokens.HasValue ? "fallback" : "not-set"));

                    AnsiConsole.MarkupLine(
                        $"  [dim]Suggested token limits: output={FormatOptionalInt(proposedOutputTokens)} ({EscapeMarkup(outputSource)}), prompt={FormatOptionalInt(proposedPromptTokens)} ({EscapeMarkup(promptSource)})[/]");
                    AnsiConsole.MarkupLine(
                        $"  [dim]Rate limits: tpm={FormatOptionalInt(deployment.SuggestedTpm)}, rpm={FormatOptionalInt(deployment.SuggestedRpm)}[/]");

                    var profileMaxOutputTokens = proposedOutputTokens;
                    var profileMaxPromptTokens = proposedPromptTokens;

                    if (effectiveMode == "each")
                    {
                        var customizePerModel = AnsiConsole.Confirm(
                            $"Customize token limits for deployment '{deployment.DeploymentName}'?",
                            false);

                        if (customizePerModel)
                        {
                            profileMaxOutputTokens = AskOptionalIntWithDefault(
                                "Max [cyan]output tokens[/]",
                                proposedOutputTokens);
                            profileMaxPromptTokens = AskOptionalIntWithDefault(
                                "Max [cyan]prompt tokens[/]",
                                proposedPromptTokens);
                        }
                    }

                    var profile = FoundryImportHelpers.BuildImportedProfile(
                        foundryAccount.Name,
                        endpoint,
                        deployment,
                        existingNames,
                        profileMaxOutputTokens,
                        profileMaxPromptTokens);

                    // Keep import idempotent per account+deployment: if canonical Foundry profile
                    // already exists for this deployment, update it instead of creating a -2 suffix.
                    var canonicalName = FoundryImportHelpers.BuildBaseProfileName(foundryAccount.Name, deployment.DeploymentName);
                    if (existingProfilesByName.TryGetValue(canonicalName, out var existingCanonical))
                    {
                        var expectedBaseUrl = $"{endpoint}/openai/deployments/{deployment.DeploymentName}";
                        var existingBaseUrl = (existingCanonical.BaseUrl ?? string.Empty).TrimEnd('/');
                        if (string.Equals(existingBaseUrl, expectedBaseUrl, StringComparison.OrdinalIgnoreCase))
                        {
                            profile.Name = canonicalName;
                        }
                    }

                    var profileName = profile.Name;

                    var upsert = ConfigManager.UpsertProfile(profile);
                    if (!upsert.Ok)
                    {
                        AnsiConsole.MarkupLine($"  [red]Failed[/] {EscapeMarkup(profileName)}");
                        continue;
                    }

                    existingNames.Add(upsert.Name);
                    profile.Name = upsert.Name;
                    existingProfilesByName[upsert.Name] = profile;
                    if (upsert.Action == "added")
                    {
                        imported += 1;
                        AnsiConsole.MarkupLine(
                            $"  [green]Added[/] {EscapeMarkup(upsert.Name)} [dim](output={FormatOptionalInt(profileMaxOutputTokens)}, prompt={FormatOptionalInt(profileMaxPromptTokens)})[/]");
                    }
                    else if (upsert.Action == "updated-equivalent")
                    {
                        AnsiConsole.MarkupLine(
                            $"  [yellow]Reused existing equivalent profile[/] {EscapeMarkup(upsert.Name)} [dim](output={FormatOptionalInt(profileMaxOutputTokens)}, prompt={FormatOptionalInt(profileMaxPromptTokens)})[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine(
                            $"  [green]Updated[/] {EscapeMarkup(upsert.Name)} [dim](output={FormatOptionalInt(profileMaxOutputTokens)}, prompt={FormatOptionalInt(profileMaxPromptTokens)})[/]");
                    }
                }
            }

            AnsiConsole.MarkupLine($"\n[green]Imported {imported} profile(s) from {scanned} deployment(s).[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error importing Foundry profiles: {EscapeMarkup(ex.Message)}[/]");
            AnsiConsole.MarkupLine("[dim]Ensure Azure CLI is installed and authenticated: az login[/]");
            return 1;
        }
    }

    class AuthEnvironmentResult
    {
        public bool UsedAzureCliToken { get; set; }
    }

    class ProcessRunResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
    }

}
