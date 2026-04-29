using CopilotX;
using Xunit;

namespace CopilotX.Tests;

public class ConfigManagerTests : IDisposable
{
    private readonly string _tempConfigDir;
    private readonly string? _previousScope;
    private readonly string? _previousConfigDir;

    public ConfigManagerTests()
    {
        _tempConfigDir = Path.Combine(Path.GetTempPath(), $"copilotx-dotnet-test-{Guid.NewGuid():N}");
        _previousScope = Environment.GetEnvironmentVariable("COPILOTX_CONFIG_SCOPE");
        _previousConfigDir = Environment.GetEnvironmentVariable("COPILOTX_CONFIG_DIR");

        Environment.SetEnvironmentVariable("COPILOTX_CONFIG_SCOPE", "global");
        Environment.SetEnvironmentVariable("COPILOTX_CONFIG_DIR", _tempConfigDir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("COPILOTX_CONFIG_SCOPE", _previousScope);
        Environment.SetEnvironmentVariable("COPILOTX_CONFIG_DIR", _previousConfigDir);

        if (Directory.Exists(_tempConfigDir))
        {
            Directory.Delete(_tempConfigDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveConfigFileFor_UsesGlobalWhenScopeGlobal()
    {
        var path = ConfigManager.ResolveConfigFileFor("/tmp/copilotx", "global", "tenant__user");

        Assert.Equal("/tmp/copilotx/config.json", path.Replace('\\', '/'));
    }

    [Fact]
    public void ResolveConfigFileFor_UsesAzureUserScopedWhenIdentityPresent()
    {
        var path = ConfigManager.ResolveConfigFileFor("/tmp/copilotx", "auto", "tenant123__user_contoso.com");

        Assert.Equal("/tmp/copilotx/config.tenant123__user_contoso.com.json", path.Replace('\\', '/'));
    }

    [Fact]
    public void ResolveConfigFileFor_FallsBackToGlobalWhenIdentityMissing()
    {
        var path = ConfigManager.ResolveConfigFileFor("/tmp/copilotx", "azure-user", null);

        Assert.Equal("/tmp/copilotx/config.json", path.Replace('\\', '/'));
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

}
