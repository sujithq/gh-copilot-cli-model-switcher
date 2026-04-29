using Spectre.Console;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

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

        try
        {
            var result = await RunCopilot(copilotArgs);

            if (result.ExitCode != 0 && envInfo.UsedAzureCliToken && IsTokenFailure(result.Output))
            {
                AnsiConsole.MarkupLine("[yellow]Detected token-related auth failure. Refreshing Azure CLI token and retrying once...[/]");
                var refreshedToken = await GetAzureCliToken(profile);
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_API_KEY", refreshedToken);
                result = await RunCopilot(copilotArgs);
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

        var startInfo = new ProcessStartInfo
        {
            FileName = "az",
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
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_API_KEY", token);
            }
            else if (!string.IsNullOrEmpty(resolvedApiKey))
            {
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_API_KEY", resolvedApiKey);
            }
            else
            {
                Environment.SetEnvironmentVariable("COPILOT_PROVIDER_API_KEY", null);
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
        var startInfo = new ProcessStartInfo
        {
            FileName = "az",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var arg in azArgs)
        {
            startInfo.ArgumentList.Add(arg);
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
            doc = await RunAzJson("cognitiveservices", "account", "list", "-o", "json");
        }
        else
        {
            doc = await RunAzJson("cognitiveservices", "account", "list", "--subscription", subscription, "-o", "json");
        }

        using (doc)
        {
            var result = new List<FoundryAccount>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty;
                var resourceGroup = item.TryGetProperty("resourceGroup", out var rgProp) ? rgProp.GetString() ?? string.Empty : string.Empty;
                var kind = item.TryGetProperty("kind", out var kindProp) ? kindProp.GetString() ?? string.Empty : string.Empty;

                var endpoint = string.Empty;
                if (item.TryGetProperty("properties", out var props) && props.TryGetProperty("endpoint", out var endpointProp))
                {
                    endpoint = endpointProp.GetString() ?? string.Empty;
                }

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
                    AnsiConsole.MarkupLine($"[yellow]Skipping {foundryAccount.Name}: {ex.Message}[/]");
                    continue;
                }

                if (deployments.Count == 0)
                {
                    continue;
                }

                AnsiConsole.MarkupLine($"\n[bold]Account:[/] {foundryAccount.Name} ([dim]{foundryAccount.ResourceGroup}[/])");
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
                        AnsiConsole.MarkupLine($"  [green]Added[/] {profileName}");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"  [red]Failed[/] {profileName}");
                    }
                }
            }

            AnsiConsole.MarkupLine($"\n[green]Imported {imported} profile(s) from {scanned} deployment(s).[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error importing Foundry profiles: {ex.Message}[/]");
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
