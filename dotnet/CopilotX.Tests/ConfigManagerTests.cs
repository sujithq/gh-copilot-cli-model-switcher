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
        _tempConfigDir = Path.Combine(Directory.GetCurrentDirectory(), $".copilot-byok-model-switcher-dotnet-test-{Guid.NewGuid():N}");
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
        Assert.Equal("https://myfoundry.openai.azure.com/openai/v1", profile.BaseUrl);
        Assert.Equal("gpt-4o", profile.Model);
        Assert.Equal("gpt-4o-prod", profile.Deployment);
        Assert.Equal("azure", profile.ProviderType);
        Assert.Null(profile.AzureCliToken);
        Assert.Null(profile.TokenScope);
        Assert.Equal("entra", profile.Authentication!.Type);
        Assert.Equal("https://ai.azure.com", profile.Authentication.Resource);
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
    public void BuildImportedProfile_NormalizesOpenAIEndpointAndSeparatesDeployment()
    {
        foreach (var endpoint in new[] { "https://example.com/", "https://example.com/openai/v1/", "https://example.com/openai/deployments/production" })
        {
            var profile = FoundryImportHelpers.BuildImportedProfile("account", endpoint,
                new FoundryDeployment { ModelName = "logical", DeploymentName = "production" }, []);
            Assert.Equal("https://example.com/openai/v1", profile.BaseUrl);
            Assert.Equal("logical", profile.Model);
            Assert.Equal("production", profile.Deployment);
            Assert.Equal("azure", profile.ProviderType);
            Assert.Equal("https://ai.azure.com", profile.Authentication!.Resource);
        }
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
        var environment = new Dictionary<string, string?>();
            CopilotX.Program.SetProviderTokenLimitEnvironment(new Profile
            {
                MaxOutputTokens = 8192,
                MaxPromptTokens = 64000
            }, environment);

            Assert.Equal("8192", environment["COPILOT_PROVIDER_MAX_OUTPUT_TOKENS"]);
            Assert.Equal("64000", environment["COPILOT_PROVIDER_MAX_PROMPT_TOKENS"]);

            CopilotX.Program.SetProviderTokenLimitEnvironment(new Profile(), environment);

            Assert.False(environment.ContainsKey("COPILOT_PROVIDER_MAX_OUTPUT_TOKENS"));
            Assert.False(environment.ContainsKey("COPILOT_PROVIDER_MAX_PROMPT_TOKENS"));
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

    [Fact]
    public void Entra_SaveAndUpsertRejectSecretsBeforeWriting()
    {
        var profile = new Profile
        {
            Name = "enterprise", Type = "byok", BaseUrl = "https://example.com", Model = "m",
            Authentication = new() { Type = "entra" }, ApiKey = "forbidden-stored-key"
        };
        Assert.Throws<InvalidOperationException>(() => ConfigManager.UpsertProfile(profile));
        Assert.Throws<InvalidOperationException>(() => ConfigManager.SaveConfig(new Config { Profiles = [profile] }));
        Assert.False(File.Exists(Path.Combine(_tempConfigDir, "config.json")));
    }

    [Fact]
    public void Entra_PersistenceAndDedupIncludeAuthenticationAndDeployment()
    {
        Profile Make(string name, string deployment, string resource) => new()
        {
            Name = name, Type = "byok", BaseUrl = "https://example.com/openai/v1", Model = "logical",
            Deployment = deployment, Authentication = new() { Type = "entra", Resource = resource, Preflight = true }
        };
        Assert.True(ConfigManager.AddProfile(Make("a", "prod", "https://ai.azure.com")));
        Assert.Equal("added", ConfigManager.UpsertProfile(Make("b", "test", "https://ai.azure.com")).Action);
        Assert.Equal("added", ConfigManager.UpsertProfile(Make("c", "prod", "https://cognitiveservices.azure.com")).Action);
        Assert.Equal("updated-equivalent", ConfigManager.UpsertProfile(Make("d", "prod", "https://ai.azure.com")).Action);
        var saved = ConfigManager.GetProfile("a")!;
        Assert.Equal("prod", saved.Deployment);
        Assert.Equal("entra", saved.Authentication!.Type);
        Assert.True(saved.Authentication.Preflight);
        Assert.Null(saved.ApiKey);
        Assert.Null(saved.ApiKeyEnv);
        Assert.Null(saved.AzureCliToken);
    }

    [Fact]
    public void Entra_LoadFailsClosedForUnknownAuthenticationKeys()
    {
        ConfigManager.EnsureConfigDir();
        File.WriteAllText(ConfigManager.GetConfigFile(),
            """{"profiles":[{"name":"bad","type":"byok","model":"m","baseUrl":"https://example.com","authentication":{"type":"entra","token":"secret-do-not-log"}}]}""");
        var error = Assert.Throws<InvalidOperationException>(() => ConfigManager.LoadConfig());
        Assert.DoesNotContain("secret-do-not-log", error.Message);
    }

    [Fact]
    public void ConfigScope_ReadsDocumentedAccountShowMetadata()
    {
        using var doc = JsonDocument.Parse("""
        {"tenantId":"Tenant-ID","user":{"name":"User@Contoso.com"}}
        """);
        Assert.Equal("tenant-id__user@contoso.com", ConfigManager.ReadAzureIdentity(doc.RootElement));
        using var empty = JsonDocument.Parse("{}");
        Assert.Null(ConfigManager.ReadAzureIdentity(empty.RootElement));
    }

    [Fact]
    public void ConfigScope_PinsSelectedFileAcrossLoginIdentityChanges()
    {
        ConfigManager.LoadConfig();
        var originalFile = ConfigManager.GetConfigFile();
        using (ConfigManager.PinConfiguration())
        {
            Environment.SetEnvironmentVariable("COPILOT_BYOK_MODEL_SWITCHER_CONFIG_DIR", Path.Combine(_tempConfigDir, "changed"));
            Assert.Equal(originalFile, ConfigManager.GetConfigFile());
            Assert.True(ConfigManager.SetLastUsed("original-profile"));
        }
        Environment.SetEnvironmentVariable("COPILOT_BYOK_MODEL_SWITCHER_CONFIG_DIR", _tempConfigDir);
        Assert.Equal("original-profile", ConfigManager.GetLastUsed());
    }

    [Fact]
    public async Task ConfigScope_InteractiveLoginCannotRedirectProfilePersistence()
    {
        const string tenant = "11111111-1111-1111-1111-111111111111";
        var previousResolver = ConfigManager.AzureIdentityResolver;
        string? currentIdentity = null;
        ConfigManager.AzureIdentityResolver = () => currentIdentity;
        try
        {
            var profile = new Profile
            {
                Name = "enterprise-origin", Type = "byok", Model = "logical", BaseUrl = "https://example.com",
                Authentication = new() { Type = "entra", Tenant = tenant }
            };
            Assert.True(ConfigManager.AddProfile(profile));
            var originalFile = ConfigManager.GetConfigFile();
            Environment.SetEnvironmentVariable("COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE", "auto");
            using (ConfigManager.PinConfiguration())
            {
                var calls = 0;
                await EnterpriseAuth.Acquire(profile, true, async args =>
                {
                    await Task.Yield();
                    calls++;
                    if (calls == 1) return new AzureResult(1, "");
                    if (args[0] == "login")
                    {
                        currentIdentity = $"{tenant}__signed-in@contoso.com";
                        return new AzureResult(0, "");
                    }
                    if (args[1] == "show")
                        return new AzureResult(0, JsonSerializer.Serialize(new { tenantId = tenant }));
                    return new AzureResult(0, JsonSerializer.Serialize(new
                    {
                        accessToken = "test-only-transient-token", expires_on = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
                    }));
                }, () => DateTimeOffset.UtcNow);
                Assert.Equal(originalFile, ConfigManager.GetConfigFile());
                Assert.True(ConfigManager.SetLastUsed(profile.Name));
                Assert.Equal(profile.Name, ConfigManager.GetProfile(profile.Name)!.Name);
            }
            var newlyResolvedFile = ConfigManager.GetConfigFile();
            Assert.NotEqual(originalFile, newlyResolvedFile);
            Assert.False(File.Exists(newlyResolvedFile));
            var original = JsonSerializer.Deserialize<Config>(File.ReadAllText(originalFile))!;
            Assert.Equal(profile.Name, original.LastUsed);
            Assert.Contains(original.Profiles, p => p.Name == profile.Name);
            Assert.DoesNotContain("test-only-transient-token", File.ReadAllText(originalFile));
        }
        finally
        {
            ConfigManager.AzureIdentityResolver = previousResolver;
            Environment.SetEnvironmentVariable("COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE", "global");
        }
    }

}
