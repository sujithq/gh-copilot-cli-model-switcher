using System.Text.Json;
using CopilotX.Models;

namespace CopilotX.Services;

/// <summary>
/// Manages reading and writing the copilotx configuration file
/// stored at <c>~/.copilotx/config.json</c>.
/// </summary>
public sealed class ConfigService
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".copilotx");

    private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>Loads the configuration from disk, returning defaults if the file does not exist.</summary>
    public Config Load()
    {
        if (!File.Exists(ConfigFile))
            return Config.CreateDefault();

        try
        {
            var json = File.ReadAllText(ConfigFile);
            return JsonSerializer.Deserialize<Config>(json, JsonOptions) ?? Config.CreateDefault();
        }
        catch
        {
            return Config.CreateDefault();
        }
    }

    /// <summary>Persists the configuration to disk, creating the directory if needed.</summary>
    public void Save(Config config)
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigFile, json + Environment.NewLine);
    }

    /// <summary>Finds a profile by name (case-sensitive).</summary>
    public static Profile? GetProfile(Config config, string name) =>
        config.Profiles.FirstOrDefault(p => p.Name == name);

    /// <summary>
    /// Returns the environment variable changes required to activate a profile.
    /// </summary>
    /// <returns>
    /// A tuple of (varsToSet, varsToUnset) where varsToSet is a dictionary
    /// of variable names to values, and varsToUnset is a list of names to remove.
    /// </returns>
    public static (Dictionary<string, string> Set, IReadOnlyList<string> Unset) BuildEnvForProfile(Profile profile)
    {
        if (profile.Type == ProfileType.Copilot)
        {
            return (
                new Dictionary<string, string>(),
                new[] { "COPILOT_PROVIDER_BASE_URL", "COPILOT_PROVIDER_API_KEY", "COPILOT_MODEL" }
            );
        }

        var set = new Dictionary<string, string>
        {
            ["COPILOT_PROVIDER_BASE_URL"] = profile.BaseUrl ?? string.Empty,
            ["COPILOT_MODEL"] = profile.Model ?? string.Empty,
        };

        if (!string.IsNullOrEmpty(profile.ApiKeyEnv))
        {
            var keyValue = Environment.GetEnvironmentVariable(profile.ApiKeyEnv);
            if (!string.IsNullOrEmpty(keyValue))
                set["COPILOT_PROVIDER_API_KEY"] = keyValue;
        }
        else if (!string.IsNullOrEmpty(profile.ApiKey))
        {
            set["COPILOT_PROVIDER_API_KEY"] = profile.ApiKey;
        }

        if (!string.IsNullOrEmpty(profile.ProviderType))
            set["COPILOT_PROVIDER_TYPE"] = profile.ProviderType;

        return (set, Array.Empty<string>());
    }
}
