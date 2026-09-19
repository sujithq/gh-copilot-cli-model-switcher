using System.Text.Json;
using System.Text.Json.Serialization;

namespace CopilotX;

public class Profile
{
    [JsonPropertyName("deployment")]
    public string? Deployment { get; set; }

    [JsonPropertyName("authentication")]
    public ProfileAuthentication? Authentication { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; set; }

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

    [JsonPropertyName("maxOutputTokens")]
    public int? MaxOutputTokens { get; set; }

    [JsonPropertyName("maxTokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens
    {
        get => null;
        set
        {
            if (!MaxOutputTokens.HasValue)
            {
                MaxOutputTokens = value;
            }
        }
    }

    [JsonPropertyName("maxPromptTokens")]
    public int? MaxPromptTokens { get; set; }

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
        ".copilot-byok-model-switcher"
    );

    private static readonly string LegacyConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".copilotx"
    );

    private static string GetConfigDir()
    {
        var configured = Environment.GetEnvironmentVariable("COPILOT_BYOK_MODEL_SWITCHER_CONFIG_DIR")
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
            // Read only CLI account metadata, not token caches; registry checks must precede az.
            var azureDir = Environment.GetEnvironmentVariable("AZURE_CONFIG_DIR");
            if (string.IsNullOrWhiteSpace(azureDir))
                azureDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".azure");
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(azureDir, "azureProfile.json")).TrimStart('\uFEFF'));
            return ReadAzureIdentity(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }

    internal static string? ReadAzureIdentity(JsonElement root)
    {
        if (!root.TryGetProperty("subscriptions", out var accounts) || accounts.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var account in accounts.EnumerateArray())
        {
            if (!account.TryGetProperty("isDefault", out var isDefault) || isDefault.ValueKind != JsonValueKind.True)
                continue;
            if (!account.TryGetProperty("tenantId", out var tenant) || tenant.ValueKind != JsonValueKind.String ||
                !account.TryGetProperty("user", out var user) || user.ValueKind != JsonValueKind.Object ||
                !user.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
                return null;
            if (string.IsNullOrWhiteSpace(tenant.GetString()) || string.IsNullOrWhiteSpace(name.GetString()))
                return null;
            return $"{SanitizeSegment(tenant.GetString()!)}__{SanitizeSegment(name.GetString()!)}";
        }
        return null;
    }

    private static readonly AsyncLocal<string?> PinnedConfigFile = new();

    internal static IDisposable PinConfiguration()
    {
        var previous = PinnedConfigFile.Value;
        PinnedConfigFile.Value = ResolveConfigFile();
        return new ConfigFileScope(previous);
    }

    private sealed class ConfigFileScope(string? previous) : IDisposable
    {
        public void Dispose() => PinnedConfigFile.Value = previous;
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
        if (PinnedConfigFile.Value is { } pinned) return pinned;
        var configDir = GetConfigDir();
        var scope = (Environment.GetEnvironmentVariable("COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE")
            ?? Environment.GetEnvironmentVariable("COPILOTX_CONFIG_SCOPE")
            ?? "auto").ToLowerInvariant();
        var identityKey = scope == "global" ? null : GetAzureIdentityKey();
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
            var config = JsonSerializer.Deserialize<Config>(json, JsonOptions)
                ?? throw new InvalidOperationException("Invalid configuration.");
            foreach (var profile in config.Profiles) EnterpriseAuth.Validate(profile);
            return config;
        }
        catch
        {
            throw new InvalidOperationException("Invalid profile configuration. Check authentication settings; credentials must not be stored in Entra profiles.");
        }
    }

    public static bool SaveConfig(Config config)
    {
        foreach (var profile in config.Profiles) EnterpriseAuth.Validate(profile);
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
            deployment = NormalizeCaseSensitive(profile.Deployment),
            authentication = profile.Authentication,
            maxOutputTokens = profile.MaxOutputTokens,
            maxPromptTokens = profile.MaxPromptTokens,
            mcpCompatServers = NormalizeMcpServers(profile)
        };

        return JsonSerializer.Serialize(keyObject);
    }

    public static ProfileUpsertResult UpsertProfile(Profile profile)
    {
        EnterpriseAuth.Validate(profile);
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
                Deployment = profile.Deployment,
                Authentication = profile.Authentication,
                ExtraFields = profile.ExtraFields,
                MaxOutputTokens = profile.MaxOutputTokens,
                MaxPromptTokens = profile.MaxPromptTokens,
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
