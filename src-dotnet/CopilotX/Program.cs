using System.CommandLine;
using System.Diagnostics;
using Spectre.Console;

namespace CopilotX;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var root = new RootCommand("copilotx - switch GitHub Copilot CLI model profiles")
        {
            BuildListCommand(),
            BuildLastCommand(),
            BuildUseCommand(),
            BuildDefaultCommand(),
            BuildAddCommand(),
            BuildConfigPathCommand(),
        };

        return await root.InvokeAsync(args);
    }

    private static Command BuildListCommand()
    {
        var cmd = new Command("list", "List profiles");
        cmd.SetHandler(async () =>
        {
            var cfg = await AppConfig.LoadAsync(CancellationToken.None);
            foreach (var p in cfg.Profiles)
            {
                var marker = p.Name == cfg.LastUsed ? "*" : " ";
                AnsiConsole.MarkupLine($"{marker} {p.Name}\t({p.Type})");
            }
        });
        return cmd;
    }

    private static Command BuildLastCommand()
    {
        var cmd = new Command("last", "Print last used profile");
        cmd.SetHandler(async () =>
        {
            var cfg = await AppConfig.LoadAsync(CancellationToken.None);
            AnsiConsole.WriteLine(string.IsNullOrWhiteSpace(cfg.LastUsed) ? "default" : cfg.LastUsed);
        });
        return cmd;
    }

    private static Command BuildUseCommand()
    {
        var profileArg = new Argument<string>("profile", "Profile name");
        var copilotArgs = new Argument<string[]>("copilotArgs") { Arity = ArgumentArity.ZeroOrMore };

        var cmd = new Command("use", "Use a profile and run copilot") { profileArg, copilotArgs };
        cmd.SetHandler(async (string profile, string[] copilotArgs) =>
        {
            var cfg = await AppConfig.LoadAsync(CancellationToken.None);
            var p = cfg.Profiles.FirstOrDefault(x => x.Name == profile);
            if (p is null)
            {
                AnsiConsole.MarkupLine($"[red]Profile not found:[/] {profile}");
                Environment.ExitCode = 1;
                return;
            }

            var env = Env.BuildForProfile(p);
            cfg.LastUsed = profile;
            await cfg.SaveAsync(CancellationToken.None);

            ExecCopilot(copilotArgs, env);
        }, profileArg, copilotArgs);

        return cmd;
    }

    private static Command BuildDefaultCommand()
    {
        var copilotArgs = new Argument<string[]>("copilotArgs") { Arity = ArgumentArity.ZeroOrMore };
        var cmd = new Command("default", "Use default Copilot mode and run copilot") { copilotArgs };
        cmd.SetHandler(async (string[] copilotArgs) =>
        {
            var cfg = await AppConfig.LoadAsync(CancellationToken.None);
            var p = cfg.Profiles.First(x => x.Name == "default");
            var env = Env.BuildForProfile(p);
            cfg.LastUsed = "default";
            await cfg.SaveAsync(CancellationToken.None);
            ExecCopilot(copilotArgs, env);
        }, copilotArgs);
        return cmd;
    }

    private static Command BuildAddCommand()
    {
        var nameArg = new Argument<string>("name", "Profile name");
        var typeOpt = new Option<string>("--type", () => "byok", "Profile type (copilot|byok|proxy)");
        var baseUrlOpt = new Option<string?>("--baseUrl", "Provider base URL");
        var modelOpt = new Option<string?>("--model", "Model name");
        var providerTypeOpt = new Option<string?>("--providerType", "Provider type for Copilot CLI (optional)");
        var apiKeyEnvOpt = new Option<string?>("--apiKeyEnv", "Env var name containing API key");
        var apiKeyOpt = new Option<string?>("--apiKey", "API key value (not recommended; stored in config)");

        var cmd = new Command("add", "Add a profile")
        {
            nameArg,
            typeOpt,
            baseUrlOpt,
            modelOpt,
            providerTypeOpt,
            apiKeyEnvOpt,
            apiKeyOpt,
        };

        cmd.SetHandler(async (string name, string type, string? baseUrl, string? model, string? providerType, string? apiKeyEnv, string? apiKey) =>
        {
            var cfg = await AppConfig.LoadAsync(CancellationToken.None);
            if (cfg.Profiles.Any(p => p.Name == name))
            {
                AnsiConsole.MarkupLine($"[red]Profile already exists:[/] {name}");
                Environment.ExitCode = 1;
                return;
            }

            if (type != "copilot" && type != "byok" && type != "proxy")
                throw new ArgumentException("--type must be copilot|byok|proxy");

            if (type != "copilot" && string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("--baseUrl is required for byok/proxy");

            if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiKeyEnv))
                throw new ArgumentException("Use only --apiKey or --apiKeyEnv");

            var p = new Profile
            {
                Name = name,
                Type = type,
                BaseUrl = baseUrl,
                Model = model,
                ProviderType = providerType,
                ApiKeyEnv = apiKeyEnv,
                ApiKey = apiKey,
            };

            cfg.Profiles.Add(p);
            await cfg.SaveAsync(CancellationToken.None);
            AnsiConsole.MarkupLine($"Added profile: [green]{name}[/]");
        }, nameArg, typeOpt, baseUrlOpt, modelOpt, providerTypeOpt, apiKeyEnvOpt, apiKeyOpt);

        return cmd;
    }

    private static Command BuildConfigPathCommand()
    {
        var cmd = new Command("config-path", "Print config path");
        cmd.SetHandler(() => AnsiConsole.WriteLine(AppConfig.ConfigPath()));
        return cmd;
    }

    private static void ExecCopilot(string[] copilotArgs, Dictionary<string, string> env)
    {
        var psi = new ProcessStartInfo("copilot")
        {
            UseShellExecute = false,
        };

        foreach (var a in copilotArgs) psi.ArgumentList.Add(a);
        foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;

        using var p = Process.Start(psi);
        if (p is null) throw new InvalidOperationException("Failed to start copilot");
        p.WaitForExit();
        Environment.ExitCode = p.ExitCode;
    }
}

