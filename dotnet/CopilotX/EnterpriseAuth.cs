using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CopilotX;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ProfileAuthentication
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
    [JsonPropertyName("tenant")]
    public string? Tenant { get; set; }
    [JsonPropertyName("resource")]
    public string? Resource { get; set; }
    [JsonPropertyName("preflight")]
    public bool Preflight { get; set; }
}

internal sealed record AzureResult(int ExitCode, string Output);
internal sealed record EntraToken(string AccessToken, DateTimeOffset ExpiresAt);

internal static class EnterpriseAuth
{
    internal const string DefaultResource = "https://ai.azure.com";
    internal static bool IsEntra(Profile profile) => profile.Authentication?.Type == "entra";

    internal static void ValidateLaunchArguments(Profile profile, IEnumerable<string> arguments)
    {
        if (IsEntra(profile) && arguments.Any(arg =>
            arg.Equals("--config-dir", StringComparison.OrdinalIgnoreCase) ||
            arg.StartsWith("--config-dir=", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Entra launches do not allow --config-dir because it can bypass the provider registry guard. Use a dedicated COPILOT_HOME without providers.json and unset COPILOT_PROVIDERS_CONFIG.");
    }

    internal static void Validate(Profile profile)
    {
        if (profile.Type is not ("copilot" or "byok" or "proxy"))
            throw new InvalidOperationException("Profile type must be copilot, byok, or proxy.");
        var auth = profile.Authentication;
        if (auth == null) return;
        if (auth.Type is not ("entra" or "apiKey"))
            throw new InvalidOperationException("Authentication type must be entra or apiKey.");
        if (profile.Type is not ("byok" or "proxy"))
            throw new InvalidOperationException("Explicit authentication requires a byok or proxy profile.");
        if (auth.Tenant != null && !Guid.TryParseExact(auth.Tenant, "D", out _))
            throw new InvalidOperationException("Authentication tenant must be a tenant UUID.");
        if (auth.Resource != null) ValidateHttps(auth.Resource);
        if (auth.Type == "entra")
        {
            ValidateHttps(profile.BaseUrl);
            if (profile.ApiKey != null || profile.ApiKeyEnv != null ||
                profile.AzureCliToken != null || profile.TokenScope != null ||
                profile.ExtraFields?.Count > 0)
                throw new InvalidOperationException("Entra profiles accept configuration only: remove stored credentials, unknown fields, and legacy authentication settings.");
            if (string.IsNullOrWhiteSpace(profile.Model))
                throw new InvalidOperationException("Entra profiles require a model.");
            if (auth.Preflight) _ = PreflightEndpoint(profile);
        }
    }

    internal static Uri ValidateHttps(string? value)
    {
        if (value == null || value.Any(char.IsWhiteSpace) || value.Contains('\\') ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrEmpty(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) || value!.Contains('?') || value.Contains('#'))
            throw new InvalidOperationException("Use an HTTPS URL without credentials, query, or fragment.");
        return uri;
    }

    internal static Dictionary<string, string?> CopyEnvironment()
    {
        var result = new Dictionary<string, string?>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            result[(string)entry.Key] = entry.Value?.ToString();
        return result;
    }

    internal static Dictionary<string, string?> CleanEnvironment(IDictionary<string, string?> inherited)
    {
        var result = new Dictionary<string, string?>(inherited, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var key in result.Keys.Where(k => k.StartsWith("COPILOT_PROVIDER_", StringComparison.OrdinalIgnoreCase) ||
                     k.Equals("COPILOT_MODEL", StringComparison.OrdinalIgnoreCase)).ToArray())
            result.Remove(key);
        return result;
    }

    internal static void GuardRegistry(IDictionary<string, string?> environment, string home)
    {
        try
        {
            if (environment.ContainsKey("COPILOT_PROVIDERS_CONFIG")) throw new InvalidOperationException();
            environment.TryGetValue("COPILOT_HOME", out var copilotHome);
            var path = Path.Combine(string.IsNullOrWhiteSpace(copilotHome) ? Path.Combine(home, ".copilot") : copilotHome, "providers.json");
            try { _ = File.GetAttributes(path); }
            catch (FileNotFoundException) { return; }
            catch (DirectoryNotFoundException) { return; }
            throw new InvalidOperationException();
        }
        catch
        {
            throw new InvalidOperationException("Provider registries can take precedence over launcher authentication. This Entra guard is stricter than Copilot CLI: all existing registries and COPILOT_PROVIDERS_CONFIG overrides are blocked. Use a dedicated COPILOT_HOME without providers.json and unset COPILOT_PROVIDERS_CONFIG. No files were modified.");
        }
    }

    internal static async Task<AzureResult> RunAzure(IReadOnlyList<string> arguments)
    {
        if (arguments.Count > 0 && arguments[0] == "login")
            return await RunAzureLogin(arguments);
        try
        {
            var info = AzureStartInfo(arguments, OperatingSystem.IsWindows());
            using var process = Process.Start(info) ?? throw new InvalidOperationException();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(output, error, process.WaitForExitAsync());
            return new AzureResult(process.ExitCode, await output);
        }
        catch
        {
            throw new InvalidOperationException("Unable to run Azure CLI. Install az, then run az login (with --tenant for your tenant).");
        }
    }

    private static async Task<AzureResult> RunAzureLogin(IReadOnlyList<string> arguments)
    {
        try
        {
            using var process = Process.Start(AzureStartInfo(arguments, OperatingSystem.IsWindows(), interactiveLogin: true))
                ?? throw new InvalidOperationException();
            await process.WaitForExitAsync();
            return new AzureResult(process.ExitCode, "");
        }
        catch
        {
            throw new InvalidOperationException("Unable to run interactive Azure sign-in. Run az login manually, then restart.");
        }
    }

    internal static ProcessStartInfo AzureStartInfo(IReadOnlyList<string> arguments, bool windows, bool interactiveLogin = false)
    {
        if (interactiveLogin && (arguments.Count == 0 || arguments[0] != "login"))
            throw new InvalidOperationException("Only Azure login may use interactive output.");
        var info = new ProcessStartInfo(windows ? "cmd.exe" : "az")
        {
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = !interactiveLogin,
            RedirectStandardError = !interactiveLogin
        };
        if (windows)
        {
            // Azure CLI ships as az.cmd on Windows. Refuse shell expansion characters.
            if (arguments.Any(arg => arg.Any(ch => ch is '"' or '%' or '!' or '^' or '&' or '|' or '<' or '>' or '\r' or '\n')))
                throw new InvalidOperationException("Azure CLI arguments contain unsupported shell characters.");
            info.Arguments = "/d /v:off /s /c \"az " + string.Join(" ", arguments.Select(arg => "\"" + arg + "\"")) + "\"";
        }
        else
        {
            foreach (var arg in arguments) info.ArgumentList.Add(arg);
        }
        return info;
    }

    internal static async Task<EntraToken> Acquire(Profile profile, bool interactive,
        Func<IReadOnlyList<string>, Task<AzureResult>> run, Func<DateTimeOffset> now)
    {
        Validate(profile);
        var auth = profile.Authentication!;
        async Task<AzureResult> SafeRun(IReadOnlyList<string> args)
        {
            try { return await run(args); }
            catch { throw new InvalidOperationException("Unable to run Azure CLI. Install az and run az login with --tenant if configured."); }
        }
        var account = await SafeRun(["account", "show", "--output", "json"]);
        if (account.ExitCode != 0)
        {
            if (!interactive) throw new InvalidOperationException("Azure CLI is not signed in. Run az login with --tenant if configured, then restart.");
            var loginArgs = new List<string> { "login", "--output", "none" };
            if (auth.Tenant != null) loginArgs.AddRange(["--tenant", auth.Tenant]);
            if ((await SafeRun(loginArgs)).ExitCode != 0) throw new InvalidOperationException("Azure sign-in failed. Run az login, then restart.");
            account = await SafeRun(["account", "show", "--output", "json"]);
        }
        if (account.ExitCode != 0) throw new InvalidOperationException("Azure CLI account could not be verified. Run az login, then restart.");
        try
        {
            using var doc = JsonDocument.Parse(account.Output);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("tenantId", out var tenantElement) ||
                tenantElement.ValueKind != JsonValueKind.String) throw new JsonException();
            var tenant = tenantElement.GetString();
            if (!Guid.TryParseExact(tenant, "D", out _)) throw new JsonException();
            CheckTenant(auth.Tenant, tenant);
        }
        catch (JsonException) { throw new InvalidOperationException("Azure CLI returned invalid account metadata."); }
        catch (KeyNotFoundException) { throw new InvalidOperationException("Azure CLI returned invalid account metadata."); }
        var args = new List<string> { "account", "get-access-token", "--resource", auth.Resource ?? DefaultResource };
        if (auth.Tenant != null) args.AddRange(["--tenant", auth.Tenant]);
        args.AddRange(["--output", "json"]);
        var response = await SafeRun(args);
        if (response.ExitCode != 0) throw new InvalidOperationException("Azure token acquisition failed. Check your tenant/resource and run az login.");
        return ParseToken(response.Output, auth.Tenant, now());
    }

    private static void CheckTenant(string? expected, string? actual)
    {
        if (expected != null && !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Azure tenant mismatch. Run az login --tenant with the configured tenant, then restart.");
    }

    internal static EntraToken ParseToken(string json, string? tenant, DateTimeOffset now)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("accessToken", out var tokenElement) ||
                tokenElement.ValueKind != JsonValueKind.String) throw new FormatException();
            var token = tokenElement.GetString();
            if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace)) throw new FormatException();
            if (root.TryGetProperty("tenant", out var responseTenant))
            {
                if (responseTenant.ValueKind != JsonValueKind.String) throw new FormatException();
                var actual = responseTenant.GetString();
                if (!Guid.TryParseExact(actual, "D", out _)) throw new FormatException();
                CheckTenant(tenant, actual);
            }
            DateTimeOffset expiry;
            if (root.TryGetProperty("expires_on", out var epoch))
            {
                var raw = epoch.ValueKind == JsonValueKind.String ? epoch.GetString() : epoch.GetRawText();
                if (!long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)) throw new FormatException();
                expiry = DateTimeOffset.FromUnixTimeSeconds(seconds);
            }
            else
            {
                var legacyExpiry = root.GetProperty("expiresOn");
                if (legacyExpiry.ValueKind != JsonValueKind.String) throw new FormatException();
                var raw = legacyExpiry.GetString();
                if (!DateTime.TryParseExact(raw, ["yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.FFFFFFF"],
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var local) ||
                    TimeZoneInfo.Local.IsInvalidTime(local) || TimeZoneInfo.Local.IsAmbiguousTime(local)) throw new FormatException();
                expiry = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Local));
            }
            EnsureTokenLifetime(expiry, now);
            return new EntraToken(token!, expiry);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or KeyNotFoundException or ArgumentException)
        {
            throw new InvalidOperationException("Azure CLI returned invalid token expiry or token metadata. Update az, sign in, and restart.");
        }
    }

    internal static void EnsureTokenLifetime(DateTimeOffset expiry, DateTimeOffset now)
    {
        if (expiry - now < TimeSpan.FromMinutes(5))
            throw new InvalidOperationException("Azure token expires in less than five minutes. Refresh with az login and restart.");
    }

    internal static string WireModel(Profile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.Deployment)) return profile.Deployment;
        if (Uri.TryCreate(profile.BaseUrl, UriKind.Absolute, out var uri))
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length - 1; i++)
                if (segments[i].Equals("deployments", StringComparison.OrdinalIgnoreCase)) return Uri.UnescapeDataString(segments[i + 1]);
        }
        return profile.Model ?? "";
    }

    internal static string NormalizeOpenAIBaseUrl(string? baseUrl)
    {
        var uri = ValidateHttps(baseUrl);
        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.Contains("/openai/deployments/", StringComparison.OrdinalIgnoreCase)) path = "/openai/v1";
        else if (!path.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase)) path += "/openai/v1";
        return uri.GetLeftPart(UriPartial.Authority) + path;
    }

    internal static Uri PreflightEndpoint(Profile profile)
    {
        var uri = ValidateHttps(profile.BaseUrl);
        var path = uri.AbsolutePath.TrimEnd('/');
        if (!path.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Preflight requires a chat-compatible /openai/v1 base URL. It will not probe another API surface; disable preflight or configure the correct endpoint.");
        return new Uri(uri.GetLeftPart(UriPartial.Authority) + path + "/chat/completions");
    }

    internal static async Task Preflight(Profile profile, string token, HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, PreflightEndpoint(profile));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = WireModel(profile), messages = new[] { new { role = "user", content = "Hi" } },
            max_completion_tokens = 1, stream = false
        }), Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        try { response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead); }
        catch { throw new InvalidOperationException("Preflight could not connect. Check endpoint, network access, and timeout."); }
        using (response)
        {
            var status = (int)response.StatusCode;
            if (status is >= 200 and < 300) return;
            throw new InvalidOperationException(status switch
            {
                401 => "Preflight HTTP 401: check tenant/resource and sign in again with az login.",
                403 => "Preflight HTTP 403: check the endpoint's data-plane role assignments and network/firewall access.",
                _ => $"Preflight HTTP {status}."
            });
        }
    }

    private static (string Executable, string? Loader) ResolveWindowsCopilot(IDictionary<string, string?> environment)
    {
        var path = environment.FirstOrDefault(item => item.Key.Equals("PATH", StringComparison.OrdinalIgnoreCase)).Value ?? "";
        var directories = path.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => directory.Trim().Trim('"'))
            .Where(directory => directory.Length > 0)
            .Select(Path.GetFullPath)
            .ToArray();
        string? Find(string name) => directories.Select(directory => Path.Combine(directory, name)).FirstOrDefault(File.Exists);

        var native = Find("copilot.exe");
        if (native != null) return (native, null);
        var shim = Find("copilot.cmd");
        if (shim != null)
        {
            var shimDirectory = Path.GetDirectoryName(shim)!;
            var loader = Path.Combine(shimDirectory, "node_modules", "@github", "copilot", "npm-loader.js");
            var adjacentNode = Path.Combine(shimDirectory, "node.exe");
            var node = File.Exists(adjacentNode) ? adjacentNode : Find("node.exe");
            if (File.Exists(loader) && node != null) return (node, loader);
        }
        throw new InvalidOperationException("Standalone Copilot was not found. Install the native copilot.exe on PATH, or install @github/copilot with npm and ensure its copilot.cmd, adjacent npm-loader.js, and node.exe are available.");
    }

    internal static ProcessStartInfo ChildStartInfo(Profile profile, string[] args, IDictionary<string, string?> environment, bool interactive, bool? windows = null)
    {
        var explicitEntra = IsEntra(profile);
        var executable = explicitEntra ? "copilot" : "gh";
        string? loader = null;
        if (explicitEntra && (windows ?? OperatingSystem.IsWindows()))
            (executable, loader) = ResolveWindowsCopilot(environment);
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardInput = !interactive,
            RedirectStandardOutput = !interactive,
            RedirectStandardError = !interactive
        };
        info.Environment.Clear();
        foreach (var item in environment) info.Environment[item.Key] = item.Value;
        if (loader != null) info.ArgumentList.Add(loader);
        if (!explicitEntra)
        {
            info.ArgumentList.Add("copilot");
            if (args.Length > 0) info.ArgumentList.Add("--");
        }
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }
}
