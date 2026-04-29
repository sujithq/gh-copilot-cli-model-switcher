using System.Text.Json;
using System.Text.Json.Serialization;

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
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".copilotx"
    );

    private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

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
        if (!Directory.Exists(ConfigDir))
        {
            Directory.CreateDirectory(ConfigDir);
        }
    }

    public static Config LoadConfig()
    {
        EnsureConfigDir();

        if (!File.Exists(ConfigFile))
        {
            SaveConfig(DefaultConfig);
            return DefaultConfig;
        }

        try
        {
            var json = File.ReadAllText(ConfigFile);
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

        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigFile, json);
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
        return ConfigFile;
    }
}
