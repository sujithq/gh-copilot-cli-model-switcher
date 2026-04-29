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
    private static readonly string DefaultConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".copilotx"
    );

    private static string GetConfigDir()
    {
        return Environment.GetEnvironmentVariable("COPILOTX_CONFIG_DIR") ?? DefaultConfigDir;
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
                FileName = "az",
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
        var scope = (Environment.GetEnvironmentVariable("COPILOTX_CONFIG_SCOPE") ?? "auto").ToLowerInvariant();
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
            return DefaultConfig;
        }

        try
        {
            var json = File.ReadAllText(configFile);
            return JsonSerializer.Deserialize<Config>(json, JsonOptions) ?? DefaultConfig;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading config: {ex.Message}");
            return DefaultConfig;
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

    public static bool AddProfile(Profile profile)
    {
        var config = LoadConfig();

        var existingIndex = config.Profiles.FindIndex(p => p.Name == profile.Name);
        if (existingIndex >= 0)
        {
            config.Profiles[existingIndex] = profile;
        }
        else
        {
            config.Profiles.Add(profile);
        }

        return SaveConfig(config);
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
