using CopilotX;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopilotX.Tests;

[TestClass]
public class ConfigManagerTests
{
    [TestMethod]
    public void ResolveConfigFileFor_UsesGlobalWhenScopeGlobal()
    {
        var path = ConfigManager.ResolveConfigFileFor("/tmp/copilotx", "global", "tenant__user");

        Assert.AreEqual("/tmp/copilotx/config.json", path.Replace('\\', '/'));
    }

    [TestMethod]
    public void ResolveConfigFileFor_UsesAzureUserScopedWhenIdentityPresent()
    {
        var path = ConfigManager.ResolveConfigFileFor("/tmp/copilotx", "auto", "tenant123__user_contoso.com");

        Assert.AreEqual("/tmp/copilotx/config.tenant123__user_contoso.com.json", path.Replace('\\', '/'));
    }

    [TestMethod]
    public void ResolveConfigFileFor_FallsBackToGlobalWhenIdentityMissing()
    {
        var path = ConfigManager.ResolveConfigFileFor("/tmp/copilotx", "azure-user", null);

        Assert.AreEqual("/tmp/copilotx/config.json", path.Replace('\\', '/'));
    }

}
