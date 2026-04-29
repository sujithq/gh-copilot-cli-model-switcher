using System.Text.Json.Serialization;

namespace CopilotX.Models;

/// <summary>Root configuration object persisted to ~/.copilotx/config.json.</summary>
public sealed class Config
{
    /// <summary>All configured profiles.</summary>
    [JsonPropertyName("profiles")]
    public List<Profile> Profiles { get; set; } = [];

    /// <summary>Name of the last-used profile.</summary>
    [JsonPropertyName("lastUsed")]
    public string? LastUsed { get; set; }

    /// <summary>Name of the default profile (used when none is specified).</summary>
    [JsonPropertyName("defaultProfile")]
    public string? DefaultProfile { get; set; }

    /// <summary>Creates a fresh default configuration.</summary>
    public static Config CreateDefault() => new()
    {
        Profiles =
        [
            new Profile { Name = "default", Type = ProfileType.Copilot, Model = "auto" }
        ],
        LastUsed = "default",
    };
}
