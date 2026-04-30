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
    }

}
