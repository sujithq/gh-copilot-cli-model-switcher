using CopilotX;
using Xunit;

namespace CopilotX.Tests;

public class ConfigManagerTests
{
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

}
