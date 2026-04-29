using System.Text.Json;
using System.Text.Json.Serialization;

namespace CopilotX;

public sealed class AppConfig
{
    [JsonPropertyName("profiles")]
    public List<Profile> Profiles { get; set; } = [new Profile { Name = "default", Type = "copilot", Model = "auto" }];

    [JsonPropertyName("lastUsed")]
    public string LastUsed { get; set; } = "default";

    public static string ConfigPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".copilotx", "config.json");
    }

    public static async Task<AppConfig> LoadAsync(CancellationToken cancellationToken)
    {
        var path = ConfigPath();
        if (!File.Exists(path)) return new AppConfig();

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions()) ?? new AppConfig();
        cfg.Profiles ??= [];
        if (cfg.Profiles.All(p => p.Name != "default"))
            cfg.Profiles.Insert(0, new Profile { Name = "default", Type = "copilot", Model = "auto" });
        if (string.IsNullOrWhiteSpace(cfg.LastUsed)) cfg.LastUsed = "default";
        return cfg;
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        var path = ConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(this, JsonOptions());
        await File.WriteAllTextAsync(path, json + "\n", cancellationToken);
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}

public sealed class Profile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

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
}

