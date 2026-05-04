using CopilotX;
using System.Text.Json;
using Xunit;

namespace CopilotX.Tests;

public class ConfigManagerTests : IDisposable
{
    private readonly string _tempConfigDir;
    private readonly string? _previousScope;
    private readonly string? _previousConfigDir;

    public ConfigManagerTests()
    {
        _tempConfigDir = Path.Combine(Path.GetTempPath(), $"copilot-byok-model-switcher-dotnet-test-{Guid.NewGuid():N}");
        _previousScope = Environment.GetEnvironmentVariable("COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE");
        _previousConfigDir = Environment.GetEnvironmentVariable("COPILOT_BYOK_MODEL_SWITCHER_CONFIG_DIR");

        Environment.SetEnvironmentVariable("COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE", "global");
        Environment.SetEnvironmentVariable("COPILOT_BYOK_MODEL_SWITCHER_CONFIG_DIR", _tempConfigDir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE", _previousScope);
        Environment.SetEnvironmentVariable("COPILOT_BYOK_MODEL_SWITCHER_CONFIG_DIR", _previousConfigDir);

        if (Directory.Exists(_tempConfigDir))
        {
            Directory.Delete(_tempConfigDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveConfigFileFor_UsesGlobalWhenScopeGlobal()
    {
        var path = ConfigManager.ResolveConfigFileFor("/tmp/copilot-byok-model-switcher", "global", "tenant__user");

        Assert.Equal("/tmp/copilot-byok-model-switcher/config.json", path.Replace('\\', '/'));
    }

    [Fact]
    public void ResolveConfigFileFor_UsesAzureUserScopedWhenIdentityPresent()
    {
        var path = ConfigManager.ResolveConfigFileFor("/tmp/copilot-byok-model-switcher", "auto", "tenant123__user_contoso.com");

        Assert.Equal("/tmp/copilot-byok-model-switcher/config.tenant123__user_contoso.com.json", path.Replace('\\', '/'));
    }

    [Fact]
    public void ResolveConfigFileFor_FallsBackToGlobalWhenIdentityMissing()
    {
        var path = ConfigManager.ResolveConfigFileFor("/tmp/copilot-byok-model-switcher", "azure-user", null);

        Assert.Equal("/tmp/copilot-byok-model-switcher/config.json", path.Replace('\\', '/'));
    }

    [Fact]
    public void LoadConfig_CreatesDefaultConfigWhenFileMissing()
    {
        var config = ConfigManager.LoadConfig();

        Assert.Equal("default", config.LastUsed);
        Assert.Contains(config.Profiles, p => p.Name == "default");
        Assert.True(File.Exists(Path.Combine(_tempConfigDir, "config.json")));
    }

    [Fact]
    public void AddProfile_UpdatesExistingProfileByName()
    {
        var first = new Profile
        {
            Name = "azure-gpt",
            Type = "byok",
            Model = "gpt-4"
        };

        var updated = new Profile
        {
            Name = "azure-gpt",
            Type = "byok",
            Model = "gpt-4.1"
        };

        Assert.True(ConfigManager.AddProfile(first));
        Assert.True(ConfigManager.AddProfile(updated));

        var profiles = ConfigManager.ListProfiles().Where(p => p.Name == "azure-gpt").ToList();
        Assert.Single(profiles);
        Assert.Equal("gpt-4.1", profiles[0].Model);
    }

    [Fact]
    public void SetLastUsed_PersistsAcrossReads()
    {
        Assert.True(ConfigManager.SetLastUsed("azure-gpt"));

        var value = ConfigManager.GetLastUsed();

        Assert.Equal("azure-gpt", value);
    }

    [Fact]
    public void MapDeployment_FallsBackToDeploymentName_WhenModelMetadataMissing()
    {
        using var doc = JsonDocument.Parse("{\"name\":\"gpt-4o-prod\"}");

        var deployment = FoundryImportHelpers.MapDeployment(doc.RootElement);

        Assert.Equal("gpt-4o-prod", deployment.DeploymentName);
        Assert.Equal("gpt-4o-prod", deployment.ModelName);
        Assert.Equal(string.Empty, deployment.ModelVersion);
    }

    [Fact]
    public void MapDeployment_ReadsSuggestedTokenLimits_FromMetadata()
    {
        using var doc = JsonDocument.Parse("""
        {
            "name": "gpt-5-prod",
            "properties": {
                "model": {
                    "name": "gpt-5",
                    "version": "2026-03-01",
                    "maxOutputTokens": 8192
                },
                "capabilities": {
                    "maxInputTokens": "128000"
                }
            }
        }
        """);

        var deployment = FoundryImportHelpers.MapDeployment(doc.RootElement);

        Assert.Equal(8192, deployment.SuggestedMaxOutputTokens);
        Assert.Equal(128000, deployment.SuggestedMaxPromptTokens);
        Assert.Equal("metadata", deployment.SuggestedMaxOutputTokensSource);
        Assert.Equal("metadata", deployment.SuggestedMaxPromptTokensSource);
    }

    [Fact]
    public void MapDeployment_UsesModelFamilyHeuristics_WhenMetadataTokenLimitsMissing()
    {
        using var doc = JsonDocument.Parse("""
        {
            "name": "gpt-4.1-prod",
            "properties": {
                "model": {
                    "name": "gpt-4.1",
                    "version": "2025-04-14"
                }
            }
        }
        """);

        var deployment = FoundryImportHelpers.MapDeployment(doc.RootElement);

        Assert.Equal(8192, deployment.SuggestedMaxOutputTokens);
        Assert.Equal(128000, deployment.SuggestedMaxPromptTokens);
        Assert.Equal("model-family", deployment.SuggestedMaxOutputTokensSource);
        Assert.Equal("model-family", deployment.SuggestedMaxPromptTokensSource);
    }

    [Fact]
    public void MapDeployment_ReadsRateLimits_ForGpt54AndKimi()
    {
        using var gptDoc = JsonDocument.Parse("""
        {
            "name": "gpt-5.4-1",
            "properties": {
                "model": { "name": "gpt-5.4", "version": "2026-03-05" },
                "rateLimits": [
                    { "key": "request", "count": 5000, "renewalPeriod": 60 },
                    { "key": "token", "count": 500000, "renewalPeriod": 60 }
                ]
            }
        }
        """);

        using var kimiDoc = JsonDocument.Parse("""
        {
            "name": "Kimi-K2.6-1",
            "properties": {
                "model": { "name": "Kimi-K2.6", "version": "2026-04-20" },
                "rateLimits": [
                    { "key": "request", "count": 50, "renewalPeriod": 60 },
                    { "key": "token", "count": 50000, "renewalPeriod": 60 }
                ]
            }
        }
        """);

        var gptDeployment = FoundryImportHelpers.MapDeployment(gptDoc.RootElement);
        var kimiDeployment = FoundryImportHelpers.MapDeployment(kimiDoc.RootElement);

        Assert.Equal(500000, gptDeployment.SuggestedTpm);
        Assert.Equal(5000, gptDeployment.SuggestedRpm);

        Assert.Equal(50000, kimiDeployment.SuggestedTpm);
        Assert.Equal(50, kimiDeployment.SuggestedRpm);
    }

    [Fact]
    public void IsApplicableAccount_AcceptsAiServicesWithFlattenedEndpoint()
    {
        using var doc = JsonDocument.Parse("{\"name\":\"myfoundry\",\"kind\":\"AIServices\",\"endpoint\":\"https://myfoundry.cognitiveservices.azure.com/\"}");

        var result = FoundryImportHelpers.IsApplicableAccount(doc.RootElement);

        Assert.True(result);
    }

    [Fact]
    public void BuildUniqueProfileName_AppendsSuffix_WhenNameAlreadyExists()
    {
        var result = FoundryImportHelpers.BuildUniqueProfileName(
            "My Foundry",
            "GPT-4o",
            new[] { "foundry-my-foundry-gpt-4o", "foundry-my-foundry-gpt-4o-2" });

        Assert.Equal("foundry-my-foundry-gpt-4o-3", result);
    }

    [Fact]
    public void BuildBaseProfileName_ReturnsDeterministicCanonicalName()
    {
        var result = FoundryImportHelpers.BuildBaseProfileName("My Foundry", "GPT-5.4-1");

        Assert.Equal("foundry-my-foundry-gpt-5-4-1", result);
    }

    [Fact]
    public void BuildImportedProfile_CreatesAzureTokenProfile()
    {
        var profile = FoundryImportHelpers.BuildImportedProfile(
            "myfoundry",
            "https://myfoundry.openai.azure.com/",
            new FoundryDeployment
            {
                DeploymentName = "gpt-4o-prod",
                ModelName = "gpt-4o",
                ModelVersion = "2024-11-20"
            },
            Array.Empty<string>());

        Assert.Equal("foundry-myfoundry-gpt-4o-prod", profile.Name);
        Assert.Equal("byok", profile.Type);
        Assert.Equal("https://myfoundry.openai.azure.com/openai/deployments/gpt-4o-prod", profile.BaseUrl);
        Assert.Equal("gpt-4o-prod", profile.Model);
        Assert.Equal("azure", profile.ProviderType);
        Assert.Equal("auto", profile.AzureCliToken);
        Assert.Equal("https://cognitiveservices.azure.com/.default", profile.TokenScope);
        Assert.Null(profile.MaxOutputTokens);
        Assert.Null(profile.MaxPromptTokens);
    }

    [Fact]
    public void BuildImportedProfile_AppliesConfiguredTokenLimits()
    {
        var profile = FoundryImportHelpers.BuildImportedProfile(
            "myfoundry",
            "https://myfoundry.openai.azure.com/",
            new FoundryDeployment
            {
                DeploymentName = "gpt-5-prod",
                ModelName = "gpt-5",
                ModelVersion = "2026-03-05"
            },
            Array.Empty<string>(),
            maxOutputTokens: 4096,
            maxPromptTokens: 64000);

        Assert.Equal(4096, profile.MaxOutputTokens);
        Assert.Equal(64000, profile.MaxPromptTokens);
    }

    [Fact]
    public void AddProfile_PersistsTokenLimitSettings()
    {
        var profile = new Profile
        {
            Name = "openai-gpt",
            Type = "byok",
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-5",
            MaxOutputTokens = 4096,
            MaxPromptTokens = 120000
        };

        Assert.True(ConfigManager.AddProfile(profile));

        var saved = ConfigManager.GetProfile("openai-gpt");

        Assert.NotNull(saved);
        Assert.Equal(4096, saved!.MaxOutputTokens);
        Assert.Equal(120000, saved.MaxPromptTokens);
    }

    [Fact]
    public void LoadConfig_ReadsLegacyMaxTokensAliasIntoMaxOutputTokens()
    {
        ConfigManager.EnsureConfigDir();
        var configPath = ConfigManager.GetConfigFile();
        File.WriteAllText(configPath, """
        {
            "profiles": [
                {
                    "name": "legacy-openai",
                    "type": "byok",
                    "baseUrl": "https://api.openai.com/v1",
                    "model": "gpt-4.1",
                    "maxTokens": 2048,
                    "maxPromptTokens": 32000
                }
            ],
            "lastUsed": "legacy-openai"
        }
        """);

        var profile = ConfigManager.GetProfile("legacy-openai");

        Assert.NotNull(profile);
        Assert.Equal(2048, profile!.MaxOutputTokens);
        Assert.Equal(32000, profile.MaxPromptTokens);
    }

    [Fact]
    public void SaveConfig_WritesCanonicalMaxOutputTokensField()
    {
        var profile = new Profile
        {
            Name = "canonical-openai",
            Type = "byok",
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-5",
            MaxOutputTokens = 8192,
            MaxPromptTokens = 64000
        };

        Assert.True(ConfigManager.AddProfile(profile));

        var configJson = File.ReadAllText(ConfigManager.GetConfigFile());

        Assert.Contains("\"maxOutputTokens\": 8192", configJson);
        Assert.DoesNotContain("\"maxTokens\": 8192", configJson);
    }

    [Fact]
    public void SetProviderTokenLimitEnvironment_SetsAndClearsEnvVars()
    {
        var previousMaxOutput = Environment.GetEnvironmentVariable("COPILOT_PROVIDER_MAX_OUTPUT_TOKENS");
        var previousMaxPrompt = Environment.GetEnvironmentVariable("COPILOT_PROVIDER_MAX_PROMPT_TOKENS");

        try
        {
            CopilotX.Program.SetProviderTokenLimitEnvironment(new Profile
            {
                MaxOutputTokens = 8192,
                MaxPromptTokens = 64000
            });

            Assert.Equal("8192", Environment.GetEnvironmentVariable("COPILOT_PROVIDER_MAX_OUTPUT_TOKENS"));
            Assert.Equal("64000", Environment.GetEnvironmentVariable("COPILOT_PROVIDER_MAX_PROMPT_TOKENS"));

            CopilotX.Program.SetProviderTokenLimitEnvironment(new Profile());

            Assert.Null(Environment.GetEnvironmentVariable("COPILOT_PROVIDER_MAX_OUTPUT_TOKENS"));
            Assert.Null(Environment.GetEnvironmentVariable("COPILOT_PROVIDER_MAX_PROMPT_TOKENS"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_MAX_OUTPUT_TOKENS", previousMaxOutput);
            Environment.SetEnvironmentVariable("COPILOT_PROVIDER_MAX_PROMPT_TOKENS", previousMaxPrompt);
        }
    }

    [Fact]
    public void FormatProfileTokenInfo_ReturnsNotSet_WhenNoLimitsConfigured()
    {
        var text = CopilotX.Program.FormatProfileTokenInfo(new Profile());

        Assert.Equal("not set", text);
    }

    [Fact]
    public void FormatProfileTokenInfo_ReturnsBothTokenValues_WhenConfigured()
    {
        var text = CopilotX.Program.FormatProfileTokenInfo(new Profile
        {
            MaxOutputTokens = 4096,
            MaxPromptTokens = 64000
        });

        Assert.Equal("output=4096, prompt=64000", text);
    }

}
