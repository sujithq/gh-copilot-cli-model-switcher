using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace CopilotX;

public class Profile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "copilot";

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("baseUrl")]
    public string? BaseUrl { get; set; }

    [JsonPropertyName("apiKeyEnv")]
    public string? ApiKeyEnv { get; set; }

    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("providerType")]
    public string? ProviderType { get; set; }

    [JsonPropertyName("azureCliToken")]
    public string? AzureCliToken { get; set; }

    [JsonPropertyName("tokenScope")]
    public string? TokenScope { get; set; }

    [JsonPropertyName("mcpCompatServers")]
    public List<string>? McpCompatServers { get; set; }
}

public class Config
{
    [JsonPropertyName("profiles")]
    public List<Profile> Profiles { get; set; } = new();

    [JsonPropertyName("lastUsed")]
    public string LastUsed { get; set; } = "default";
}

public class ConfigManager
{
    public class ProfileUpsertResult
    {
        public bool Ok { get; set; }
        public string Action { get; set; } = "added";
        public string Name { get; set; } = string.Empty;
    }

    private static readonly string DefaultConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".gh-copilot-byok"
    );

    private static readonly string LegacyConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".copilotx"
    );

    private static string GetConfigDir()
    {
        var configured = Environment.GetEnvironmentVariable("GH_COPILOT_BYOK_CONFIG_DIR")
            ?? Environment.GetEnvironmentVariable("COPILOTX_CONFIG_DIR");

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        if (!Directory.Exists(DefaultConfigDir) && Directory.Exists(LegacyConfigDir))
        {
            return LegacyConfigDir;
        }

        return DefaultConfigDir;
    }

    private static string GetAzureCliCommand()
    {
        return OperatingSystem.IsWindows() ? "az.cmd" : "az";
    }

    private static string SanitizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var chars = value
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) || ch == '@' || ch == '.' || ch == '_' || ch == '-' ? ch : '_')
            .ToArray();

        return new string(chars);
    }

    private static string? GetAzureIdentityKey()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = GetAzureCliCommand(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = false
            };

            startInfo.ArgumentList.Add("account");
            startInfo.ArgumentList.Add("show");
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add("json");

            var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;

            var tenantId = root.TryGetProperty("tenantId", out var tenantProp)
                ? SanitizeSegment(tenantProp.GetString() ?? string.Empty)
                : string.Empty;

            var userName = string.Empty;
            if (root.TryGetProperty("user", out var userObj) && userObj.TryGetProperty("name", out var nameProp))
            {
                userName = SanitizeSegment(nameProp.GetString() ?? string.Empty);
            }

            if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(userName))
            {
                return null;
            }

            return $"{tenantId}__{userName}";
        }
        catch
        {
            return null;
        }
    }

    internal static string ResolveConfigFileFor(string configDir, string scope, string? identityKey)
    {
        var normalizedScope = (scope ?? "auto").ToLowerInvariant();

        if (normalizedScope == "global")
        {
            return Path.Combine(configDir, "config.json");
        }

        if ((normalizedScope == "azure-user" || normalizedScope == "auto")
            && !string.IsNullOrWhiteSpace(identityKey))
        {
            return Path.Combine(configDir, $"config.{identityKey}.json");
        }

        return Path.Combine(configDir, "config.json");
    }

    private static string ResolveConfigFile()
    {
        var configDir = GetConfigDir();
        var scope = (Environment.GetEnvironmentVariable("GH_COPILOT_BYOK_CONFIG_SCOPE")
            ?? Environment.GetEnvironmentVariable("COPILOTX_CONFIG_SCOPE")
            ?? "auto").ToLowerInvariant();
        var identityKey = GetAzureIdentityKey();
        return ResolveConfigFileFor(configDir, scope, identityKey);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Config DefaultConfig = new()
    {
        Profiles = new List<Profile>
        {
            new Profile
            {
                Name = "default",
                Type = "copilot",
                Model = "auto"
            }
        },
        LastUsed = "default"
    };

    public static void EnsureConfigDir()
    {
        var configDir = GetConfigDir();
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }
    }

    public static Config LoadConfig()
    {
        EnsureConfigDir();

        var configFile = ResolveConfigFile();

        if (!File.Exists(configFile))
        {
            SaveConfig(DefaultConfig);
            return JsonSerializer.Deserialize<Config>(JsonSerializer.Serialize(DefaultConfig, JsonOptions), JsonOptions)!;
        }

        try
        {
            var json = File.ReadAllText(configFile);
            return JsonSerializer.Deserialize<Config>(json, JsonOptions) ?? JsonSerializer.Deserialize<Config>(JsonSerializer.Serialize(DefaultConfig, JsonOptions), JsonOptions)!;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading config: {ex.Message}");
            return JsonSerializer.Deserialize<Config>(JsonSerializer.Serialize(DefaultConfig, JsonOptions), JsonOptions)!;
        }
    }

    public static bool SaveConfig(Config config)
    {
        EnsureConfigDir();

        var configFile = ResolveConfigFile();

        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(configFile, json);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error saving config: {ex.Message}");
            return false;
        }
    }

    public static Profile? GetProfile(string name)
    {
        var config = LoadConfig();
        return config.Profiles.FirstOrDefault(p => p.Name == name);
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string NormalizeCaseSensitive(string? value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static List<string> NormalizeMcpServers(Profile profile)
    {
        return (profile.McpCompatServers ?? new List<string>())
            .Select(Normalize)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
    }

    private static string BuildProfileSettingsKey(Profile profile)
    {
        var keyObject = new
        {
            type = Normalize(profile.Type),
            model = Normalize(profile.Model),
            baseUrl = Normalize(profile.BaseUrl),
            apiKeyEnv = NormalizeCaseSensitive(profile.ApiKeyEnv),
            apiKey = NormalizeCaseSensitive(profile.ApiKey),
            providerType = Normalize(profile.ProviderType),
            azureCliToken = Normalize(profile.AzureCliToken),
            tokenScope = Normalize(profile.TokenScope),
            mcpCompatServers = NormalizeMcpServers(profile)
        };

        return JsonSerializer.Serialize(keyObject);
    }

    public static ProfileUpsertResult UpsertProfile(Profile profile)
    {
        var config = LoadConfig();
        var incomingName = (profile.Name ?? string.Empty).Trim();

        var existingByNameIndex = config.Profiles.FindIndex(p =>
            string.Equals(p.Name, incomingName, StringComparison.OrdinalIgnoreCase));

        if (existingByNameIndex >= 0)
        {
            config.Profiles[existingByNameIndex] = profile;
            return new ProfileUpsertResult
            {
                Ok = SaveConfig(config),
                Action = "updated-by-name",
                Name = incomingName
            };
        }

        var incomingKey = BuildProfileSettingsKey(profile);
        var equivalentIndex = config.Profiles.FindIndex(p => BuildProfileSettingsKey(p) == incomingKey);

        if (equivalentIndex >= 0)
        {
            var existingName = config.Profiles[equivalentIndex].Name;
            config.Profiles[equivalentIndex] = new Profile
            {
                Name = existingName,
                Type = profile.Type,
                Model = profile.Model,
                BaseUrl = profile.BaseUrl,
                ApiKeyEnv = profile.ApiKeyEnv,
                ApiKey = profile.ApiKey,
                ProviderType = profile.ProviderType,
                AzureCliToken = profile.AzureCliToken,
                TokenScope = profile.TokenScope,
                McpCompatServers = profile.McpCompatServers
            };

            return new ProfileUpsertResult
            {
                Ok = SaveConfig(config),
                Action = "updated-equivalent",
                Name = existingName
            };
        }

        config.Profiles.Add(profile);
        return new ProfileUpsertResult
        {
            Ok = SaveConfig(config),
            Action = "added",
            Name = incomingName
        };
    }

    public static bool AddProfile(Profile profile)
    {
        return UpsertProfile(profile).Ok;
    }

    public static (bool Ok, int Removed) RemoveProfiles(IEnumerable<string> names)
    {
        var config = LoadConfig();
        var targets = new HashSet<string>(
            names.Select(n => (n ?? string.Empty).Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n)),
            StringComparer.OrdinalIgnoreCase);

        if (targets.Count == 0)
        {
            return (true, 0);
        }

        var before = config.Profiles.Count;
        config.Profiles = config.Profiles
            .Where(p => p.Name.Equals("default", StringComparison.OrdinalIgnoreCase) || !targets.Contains(p.Name))
            .ToList();

        var removed = before - config.Profiles.Count;

        if (removed > 0 && targets.Contains(config.LastUsed))
        {
            config.LastUsed = config.Profiles.Any(p => p.Name.Equals("default", StringComparison.OrdinalIgnoreCase))
                ? "default"
                : (config.Profiles.FirstOrDefault()?.Name ?? "default");
        }

        return (SaveConfig(config), removed);
    }

    public static List<Profile> ListProfiles()
    {
        var config = LoadConfig();
        return config.Profiles;
    }

    public static bool SetLastUsed(string name)
    {
        var config = LoadConfig();
        config.LastUsed = name;
        return SaveConfig(config);
    }

    public static string GetLastUsed()
    {
        var config = LoadConfig();
        return config.LastUsed;
    }

    public static string GetConfigFile()
    {
        return ResolveConfigFile();
    }
}
