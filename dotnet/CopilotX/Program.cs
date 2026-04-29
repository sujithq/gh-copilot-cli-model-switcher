using Spectre.Console;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CopilotX;

class Program
{
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
        table.AddRow("[cyan]import-foundry[/]", "Import profiles from Foundry/Azure OpenAI deployments");
        table.AddRow("[cyan]help[/]", "Show this help message");

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[dim]Examples:[/]");
        AnsiConsole.MarkupLine("  copilotx list");
        AnsiConsole.MarkupLine("  copilotx use azure-gpt suggest \"create a function\"");
        AnsiConsole.MarkupLine("  copilotx last");
        AnsiConsole.MarkupLine("  copilotx add");
        AnsiConsole.MarkupLine("  copilotx import-foundry --mode each");

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

        var profileList = profiles.ToList();
        for (int i = 0; i < profileList.Count; i++)
        {
            var profile = profileList[i];
            var marker = profile.Name == lastUsed ? "[green]*[/]" : " ";
            var baseUrl = profile.BaseUrl ?? "N/A";
            var model = profile.Model ?? "N/A";

            table.AddRow(
                $"[dim]{i + 1}[/]",
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

        // Prompt to select a profile (only if stdin is interactive)
        if (profileList.Count > 0 && Console.IsInputRedirected == false)
        {
            AnsiConsole.MarkupLine("");
            while (true)
            {
                var input = AnsiConsole.Ask<string>("[yellow]Select profile # (or press Enter to exit):[/] ").Trim();
                if (string.IsNullOrEmpty(input))
                {
                    break;
                }

                if (int.TryParse(input, out var selection) && selection > 0 && selection <= profileList.Count)
                {
                    var selectedProfile = profileList[selection - 1];
                    return UseCommand(new[] { selectedProfile.Name });
                }

                AnsiConsole.MarkupLine("[red]Invalid selection. Please try again.[/]");
            }
        }

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
            AnsiConsole.MarkupLine($"[red]Error: Profile '{profileName}' not found[/]");
            AnsiConsole.MarkupLine("Use 'copilotx list' to see available profiles.");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Using profile:[/] {profile.Name} ([dim]{profile.Type}[/])");

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

        var effectiveCopilotArgs = BuildCopilotArgs(profile, copilotArgs);

        try
        {
            var result = await RunCopilot(effectiveCopilotArgs);

            if (result.ExitCode != 0 && envInfo.UsedAzureCliToken && IsTokenFailure(result.Output))
            {
                AnsiConsole.MarkupLine("[yellow]Detected token-related auth failure. Refreshing Azure CLI token and retrying once...[/]");
                var refreshedToken = await GetAzureCliToken(profile);
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_API_KEY", null);
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_BEARER_TOKEN", refreshedToken);
                result = await RunCopilot(effectiveCopilotArgs);
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
        var disableCompat = (Environment.GetEnvironmentVariable("COPILOTX_DISABLE_MCP_COMPAT") ?? string.Empty)
            .Trim()
            .Equals("off", StringComparison.OrdinalIgnoreCase);

        var args = new List<string>(copilotArgs);

        // Azure BYOK providers can exceed tool-count limits when many MCP servers are present.
        if (!disableCompat && (profile.Type == "byok" || profile.Type == "proxy") && IsAzureProfile(profile))
        {
            var hasManualMcpControls = args.Any(a =>
                a.Equals("--disable-mcp-server", StringComparison.OrdinalIgnoreCase)
                || a.Equals("--disable-builtin-mcps", StringComparison.OrdinalIgnoreCase)
                || a.Equals("--available-tools", StringComparison.OrdinalIgnoreCase));

            if (!hasManualMcpControls)
            {
                var prefix = new List<string>
                {
                    "--disable-builtin-mcps",
                    "--disable-mcp-server", "foundry-mcp",
                    "--disable-mcp-server", "context7",
                    "--disable-mcp-server", "msx-mcp",
                    "--disable-mcp-server", "azure",
                    "--disable-mcp-server", "workiq",
                    "--disable-mcp-server", "powerbi-remote"
                };

                AnsiConsole.MarkupLine("[dim]Applying Azure BYOK MCP compatibility mode to avoid provider tool-limit errors.[/]");
                return prefix.Concat(args).ToArray();
            }
        }

        return args.ToArray();
    }

    static bool IsAzureProfile(Profile profile)
    {
        var baseUrl = profile.BaseUrl?.ToLowerInvariant() ?? string.Empty;
        var providerType = profile.ProviderType?.ToLowerInvariant() ?? string.Empty;
        return baseUrl.Contains(".openai.azure.com") || providerType == "azure";
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

    static async Task<AuthEnvironmentResult> SetEnvironmentForProfile(Profile profile)
    {
        if (profile.Type == "copilot")
        {
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_BASE_URL", null);
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_API_KEY", null);
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_BEARER_TOKEN", null);
            Environment.SetEnvironmentVariable("COPILOT_MODEL", null);
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_TYPE", null);
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
                Environment.SetEnvironmentVariable("COPILOT_MODEL", profile.Model);
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

    static async Task<ProcessRunResult> RunCopilot(string[] copilotArgs)
    {
        // If no args provided, show a hint that we're entering interactive mode
        if (copilotArgs.Length == 0)
        {
            AnsiConsole.MarkupLine("[dim]Launching gh copilot in interactive mode. Type your question below:[/]");
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
        foreach (var arg in copilotArgs)
        {
            startInfo.ArgumentList.Add(arg);
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
            AnsiConsole.MarkupLine("[cyan]Examples:[/]");
            AnsiConsole.MarkupLine("  copilotx import-foundry --mode each");
            AnsiConsole.MarkupLine("  copilotx import-foundry --all");
            AnsiConsole.MarkupLine("  copilotx import-foundry --account myfoundry --resource-group my-rg --all");
            return 0;
        }

        var options = ParseOptions(args);
        options.TryGetValue("account", out var account);
        options.TryGetValue("resource-group", out var resourceGroup);
        options.TryGetValue("subscription", out var subscription);
        options.TryGetValue("mode", out var mode);

        var addAll = options.ContainsKey("all");
        var effectiveMode = addAll ? "all" : (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (effectiveMode != "all" && effectiveMode != "each")
        {
            effectiveMode = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Import mode:")
                    .AddChoices(new[] { "each", "all" }));
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
            var existingNames = new HashSet<string>(ConfigManager.ListProfiles().Select(p => p.Name), StringComparer.OrdinalIgnoreCase);

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

                    var profile = FoundryImportHelpers.BuildImportedProfile(foundryAccount.Name, endpoint, deployment, existingNames);
                    var profileName = profile.Name;

                    if (ConfigManager.AddProfile(profile))
                    {
                        imported += 1;
                        existingNames.Add(profileName);
                        AnsiConsole.MarkupLine($"  [green]Added[/] {EscapeMarkup(profileName)}");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"  [red]Failed[/] {EscapeMarkup(profileName)}");
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
