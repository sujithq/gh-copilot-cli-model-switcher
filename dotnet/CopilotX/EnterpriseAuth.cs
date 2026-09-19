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
            throw new InvalidOperationException("Entra launches do not allow --config-dir because it can bypass the provider registry guard. Use COPILOT_HOME or COPILOT_PROVIDERS_CONFIG so the launcher can validate the selected registry.");
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
        ValidateHttps(profile.BaseUrl);
        if (auth.Tenant != null && !Guid.TryParseExact(auth.Tenant, "D", out _))
            throw new InvalidOperationException("Authentication tenant must be a tenant UUID.");
        if (auth.Resource != null) ValidateHttps(auth.Resource);
        if (auth.Type == "entra")
        {
            if (profile.ApiKey != null || profile.ApiKeyEnv != null ||
                profile.AzureCliToken != null || profile.TokenScope != null ||
                profile.ExtraFields?.Count > 0)
                throw new InvalidOperationException("Entra profiles accept configuration only: remove stored credentials, unknown fields, and legacy authentication settings.");
            if (string.IsNullOrWhiteSpace(profile.Model))
                throw new InvalidOperationException("Entra profiles require a model.");
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
        environment.TryGetValue("COPILOT_PROVIDERS_CONFIG", out var configured);
        environment.TryGetValue("COPILOT_HOME", out var copilotHome);
        var path = !string.IsNullOrWhiteSpace(configured) ? configured :
            Path.Combine(string.IsNullOrWhiteSpace(copilotHome) ? Path.Combine(home, ".copilot") : copilotHome, "providers.json");
        try
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch (FileNotFoundException) { if (string.IsNullOrWhiteSpace(configured)) return; else throw; }
            catch (DirectoryNotFoundException) { if (string.IsNullOrWhiteSpace(configured)) return; else throw; }
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidOperationException();
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Name is not ("providers" or "models")) throw new InvalidOperationException();
                var value = property.Value;
                if (value.ValueKind == JsonValueKind.Object && !value.EnumerateObject().Any()) continue;
                if (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0) continue;
                throw new InvalidOperationException();
            }
        }
        catch
        {
            throw new InvalidOperationException("Provider registry takes precedence over launcher authentication. Use a separate valid empty registry ({\"providers\":[]}) via COPILOT_PROVIDERS_CONFIG. Existing files were not modified.");
        }
    }

    internal static async Task<AzureResult> RunAzure(IReadOnlyList<string> arguments)
    {
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

    internal static ProcessStartInfo AzureStartInfo(IReadOnlyList<string> arguments, bool windows)
    {
        var info = new ProcessStartInfo(windows ? "cmd.exe" : "az")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
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
            if (expiry - now < TimeSpan.FromMinutes(5))
                throw new InvalidOperationException("Azure token expires in less than five minutes. Refresh with az login and restart.");
            return new EntraToken(token!, expiry);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or KeyNotFoundException or ArgumentException)
        {
            throw new InvalidOperationException("Azure CLI returned invalid token expiry or token metadata. Update az, sign in, and restart.");
        }
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

    internal static Uri PreflightEndpoint(Profile profile)
    {
        var uri = ValidateHttps(profile.BaseUrl);
        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.Contains("/openai/deployments/", StringComparison.OrdinalIgnoreCase)) path = "/openai/v1";
        else if (!path.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase)) path += "/openai/v1";
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

    internal static ProcessStartInfo ChildStartInfo(Profile profile, string[] args, IDictionary<string, string?> environment, bool interactive)
    {
        var explicitEntra = IsEntra(profile);
        var info = new ProcessStartInfo(explicitEntra ? "copilot" : "gh")
        {
            UseShellExecute = false,
            RedirectStandardInput = !interactive,
            RedirectStandardOutput = !interactive,
            RedirectStandardError = !interactive
        };
        info.Environment.Clear();
        foreach (var item in environment) info.Environment[item.Key] = item.Value;
        if (!explicitEntra)
        {
            info.ArgumentList.Add("copilot");
            if (args.Length > 0) info.ArgumentList.Add("--");
        }
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }
}
