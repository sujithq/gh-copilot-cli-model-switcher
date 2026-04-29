using System.Text.Json.Serialization;

namespace CopilotX.Models;

/// <summary>Profile type discriminator.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProfileType
{
    /// <summary>Use the built-in GitHub Copilot model (no custom endpoint).</summary>
    Copilot,

    /// <summary>Bring Your Own Key – connect to an external OpenAI-compatible endpoint.</summary>
    Byok,
}

/// <summary>Represents a single named model profile.</summary>
public sealed class Profile
{
    /// <summary>Unique profile name used on the CLI (e.g. "default", "azure-gpt").</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Profile type: copilot or byok.</summary>
    [JsonPropertyName("type")]
    public ProfileType Type { get; set; } = ProfileType.Copilot;

    /// <summary>Model identifier (e.g. "auto", "gpt-4o", "llama3").</summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    // ── BYOK fields ─────────────────────────────────────────────────────────

    /// <summary>Base URL of the OpenAI-compatible endpoint.</summary>
    [JsonPropertyName("baseUrl")]
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Name of the environment variable that holds the API key
    /// (preferred over storing the key inline).
    /// </summary>
    [JsonPropertyName("apiKeyEnv")]
    public string? ApiKeyEnv { get; set; }

    /// <summary>Inline API key (stored in plain text – use apiKeyEnv when possible).</summary>
    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; set; }

    /// <summary>Optional provider type hint passed as COPILOT_PROVIDER_TYPE (e.g. "azure").</summary>
    [JsonPropertyName("providerType")]
    public string? ProviderType { get; set; }
}
