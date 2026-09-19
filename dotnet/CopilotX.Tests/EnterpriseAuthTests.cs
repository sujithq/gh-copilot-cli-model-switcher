using System.Net;
using System.Text.Json;
using CopilotX;
using Xunit;

namespace CopilotX.Tests;

public sealed class EnterpriseAuthTests : IDisposable
{
    const string Tenant = "11111111-1111-1111-1111-111111111111";
    const string OtherTenant = "22222222-2222-2222-2222-222222222222";
    const string Secret = "test-only-sensitive-token";
    static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1800000000);
    readonly string directory = Path.Combine(Directory.GetCurrentDirectory(), ".entra-tests-" + Guid.NewGuid().ToString("N"));
    public EnterpriseAuthTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, true);
    static Profile Profile() => new()
    {
        Name = "enterprise", Type = "byok", Model = "logical-model", Deployment = "production",
        BaseUrl = "https://example.openai.azure.com/openai/v1",
        Authentication = new ProfileAuthentication { Type = "entra", Tenant = Tenant }
    };
    static string TokenJson(long seconds = 3600, string tenant = Tenant) => JsonSerializer.Serialize(new
    {
        accessToken = Secret, expires_on = (Now.ToUnixTimeSeconds() + seconds).ToString(), tenant
    });
    static AzureResult Account(string tenant = Tenant) => new(0, JsonSerializer.Serialize(new { tenantId = tenant }));
    Dictionary<string, string?> EnvironmentFor(string? content = null)
    {
        var registry = Path.Combine(directory, "providers.json");
        if (content != null) File.WriteAllText(registry, content);
        return new() { ["COPILOT_HOME"] = directory, ["PATH"] = "unchanged" };
    }
    static Func<IReadOnlyList<string>, Task<AzureResult>> Azure(List<string[]> calls, params AzureResult[] results)
    {
        var queue = new Queue<AzureResult>(results);
        return args =>
        {
            calls.Add(args.ToArray());
            return Task.FromResult(queue.Dequeue());
        };
    }

    [Fact]
    public void Validate_RejectsConflictingEntraCredentialsAndUnknownKeys()
    {
        foreach (var field in new[] { "\"apiKey\":\"secret\"", "\"apiKeyEnv\":\"KEY\"", "\"azureCliToken\":\"auto\"",
                     "\"tokenScope\":\"scope\"", "\"accessToken\":\"secret\"", "\"bearerToken\":\"secret\"" })
        {
            var profile = JsonSerializer.Deserialize<Profile>("{" + field +
                ",\"type\":\"byok\",\"model\":\"m\",\"baseUrl\":\"https://example.com\",\"authentication\":{\"type\":\"entra\"}}")!;
            Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.Validate(profile));
        }
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Profile>(
            """{"authentication":{"type":"entra","accessToken":"secret"}}"""));
    }

    [Fact]
    public void Validate_RejectsInvalidAuthEndpointTenantAndResource()
    {
        foreach (var endpoint in new[] { "http://example.com", "https://user@example.com", "https://example.com?q=x", "https://example.com/#x", "https://example.com?" })
        {
            var p = Profile(); p.BaseUrl = endpoint;
            Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.Validate(p));
        }
        var profile = Profile(); profile.Authentication!.Tenant = "contoso.com";
        Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.Validate(profile));
        profile = Profile(); profile.Authentication!.Resource = "http://example.com";
        Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.Validate(profile));
        profile = Profile(); profile.Authentication!.Type = "unknown";
        Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.Validate(profile));
        profile = Profile(); profile.Type = "copilot";
        Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.Validate(profile));
    }

    [Fact]
    public void Validate_AllowsLegacyLocalEndpointsAndExplicitApiKey()
    {
        EnterpriseAuth.Validate(new Profile { Type = "proxy", BaseUrl = "http://localhost:8080", ApiKey = "key" });
        var profile = Profile(); profile.Authentication!.Type = "apiKey"; profile.ApiKeyEnv = "KEY"; profile.BaseUrl = "http://localhost:11434/v1";
        EnterpriseAuth.Validate(profile);
    }

    [Fact]
    public async Task Acquire_UsesResourceTenantAndJsonWithDefaultAudience()
    {
        var calls = new List<string[]>();
        var token = await EnterpriseAuth.Acquire(Profile(), false, Azure(calls, Account(), new(0, TokenJson())), () => Now);
        Assert.Equal(Secret, token.AccessToken);
        Assert.Equal(Now.AddHours(1), token.ExpiresAt);
        Assert.Equal(new[] { "account", "get-access-token", "--resource", "https://ai.azure.com", "--tenant", Tenant, "--output", "json" }, calls[1]);
    }

    [Fact]
    public async Task Acquire_HonorsCognitiveServicesResourceOverride()
    {
        var profile = Profile(); profile.Authentication!.Resource = "https://cognitiveservices.azure.com";
        var calls = new List<string[]>();
        await EnterpriseAuth.Acquire(profile, false, Azure(calls, Account(), new(0, TokenJson())), () => Now);
        Assert.Contains("https://cognitiveservices.azure.com", calls[1]);
    }

    [Fact]
    public async Task Acquire_LogsInInteractivelyAndRechecksTenant()
    {
        var calls = new List<string[]>();
        await EnterpriseAuth.Acquire(Profile(), true, Azure(calls, new(1, Secret), new(0, Secret), Account(), new(0, TokenJson())), () => Now);
        Assert.Equal(new[] { "login", "--output", "none", "--tenant", Tenant }, calls[1]);
        Assert.Equal("show", calls[2][1]);
        Assert.Equal(4, calls.Count);
    }

    [Fact]
    public async Task Acquire_DoesNotLoginNoninteractivelyOrExposeOutput()
    {
        var calls = new List<string[]>();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => EnterpriseAuth.Acquire(
            Profile(), false, Azure(calls, new AzureResult(1, Secret)), () => Now));
        Assert.Contains("az login", error.Message);
        Assert.DoesNotContain(Secret, error.Message);
        Assert.Single(calls);
    }

    [Fact]
    public async Task Acquire_MissingCliErrorIsActionableAndSanitized()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => EnterpriseAuth.Acquire(Profile(), false,
            _ => throw new System.ComponentModel.Win32Exception(Secret), () => Now));
        Assert.Contains("Install az", error.Message);
        Assert.DoesNotContain(Secret, error.Message);
    }

    [Fact]
    public void AzureRunner_WindowsBatchInvocationRejectsShellExpansion()
    {
        var args = new[] { "account", "get-access-token", "--resource", "https://ai.azure.com", "--output", "json" };
        Assert.Equal("az", EnterpriseAuth.AzureStartInfo(args, false).FileName);
        var windows = EnterpriseAuth.AzureStartInfo(args, true);
        Assert.Equal("cmd.exe", windows.FileName);
        Assert.Contains("\"https://ai.azure.com\"", windows.Arguments);
        Assert.True(windows.RedirectStandardError);
        foreach (var suffix in new[] { "%SECRET%", "!SECRET!", "\"", "&command", "|command", "^", "<file", ">file", "\r", "\n" })
            Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.AzureStartInfo(["--resource", "https://example.com/" + suffix], true));
    }

    [Fact]
    public void AzureRunner_LoginInheritsTerminalButTokenOutputIsAlwaysCaptured()
    {
        foreach (var windows in new[] { false, true })
        {
            var login = EnterpriseAuth.AzureStartInfo(["login", "--output", "none", "--tenant", Tenant], windows, interactiveLogin: true);
            Assert.False(login.UseShellExecute);
            Assert.False(login.RedirectStandardInput);
            Assert.False(login.RedirectStandardOutput);
            Assert.False(login.RedirectStandardError);
            var token = EnterpriseAuth.AzureStartInfo(["account", "get-access-token", "--output", "json"], windows);
            Assert.True(token.RedirectStandardOutput);
            Assert.True(token.RedirectStandardError);
            Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.AzureStartInfo(
                ["account", "get-access-token"], windows, interactiveLogin: true));
        }
    }

    [Fact]
    public void Launch_RejectsAlternateConfigDirectoryBeforeEntraAcquisition()
    {
        foreach (var args in new[]
        {
            new[] { "--config-dir", "alternate" },
            new[] { "--config-dir=alternate" },
            new[] { "-p", "prompt", "--config-dir=" }
        })
        {
            var error = Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.ValidateLaunchArguments(Profile(), args));
            Assert.Contains("COPILOT_HOME", error.Message);
            Assert.Contains("COPILOT_PROVIDERS_CONFIG", error.Message);
            EnterpriseAuth.ValidateLaunchArguments(new Profile(), args);
        }
        EnterpriseAuth.ValidateLaunchArguments(Profile(), ["-p", "normal prompt"]);
    }

    [Fact]
    public async Task Acquire_StopsBeforeTokenForTenantMismatch()
    {
        var calls = new List<string[]>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => EnterpriseAuth.Acquire(Profile(), true,
            Azure(calls, Account(OtherTenant)), () => Now));
        Assert.Single(calls);
    }

    [Fact]
    public async Task Acquire_StopsAfterLoginTenantMismatch()
    {
        var calls = new List<string[]>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => EnterpriseAuth.Acquire(Profile(), true,
            Azure(calls, new(1, ""), new(0, ""), Account(OtherTenant)), () => Now));
        Assert.Equal(3, calls.Count);
    }

    [Fact]
    public async Task Acquire_RejectsMalformedAccountWithoutLeakingOutput()
    {
        foreach (var json in new[] { Secret, "{}", "[]", """{"tenantId":17}""" })
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => EnterpriseAuth.Acquire(Profile(), false,
                Azure(new(), new AzureResult(0, json)), () => Now));
            Assert.DoesNotContain(Secret, error.Message);
        }
    }

    [Fact]
    public void Token_RejectsEmptyMalformedExpiredShortAndWrongTenant()
    {
        foreach (var json in new[]
        {
            Secret, "{}", "[]", """{"accessToken":7}""",
            """{"accessToken":"","expires_on":"1800003600"}""",
            """{"accessToken":"secret","expires_on":"invalid"}""",
            """{"accessToken":"secret","expires_on":999999999999999999}""",
            """{"accessToken":"secret","expiresOn":null}""",
            TokenJson(-1), TokenJson(299), TokenJson(3600, OtherTenant)
        })
        {
            var error = Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.ParseToken(json, Tenant, Now));
            Assert.DoesNotContain(Secret, error.Message);
        }
        Assert.Equal(Now.AddMinutes(5), EnterpriseAuth.ParseToken(TokenJson(300), Tenant, Now).ExpiresAt);
    }

    [Fact]
    public void Token_PrefersUtcEpochAndFallsBackToLocalDateTime()
    {
        var epoch = JsonSerializer.Serialize(new { accessToken = Secret, expires_on = Now.AddHours(1).ToUnixTimeSeconds(), expiresOn = "bad-local-time" });
        Assert.Equal(Now.AddHours(1), EnterpriseAuth.ParseToken(epoch, null, Now).ExpiresAt);
        var local = Now.AddHours(1).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(Now.AddHours(1), EnterpriseAuth.ParseToken(JsonSerializer.Serialize(new { accessToken = Secret, expiresOn = local }), null, Now).ExpiresAt);
    }

    [Fact]
    public void Registry_RejectsEveryExistingRegistryWithoutChangingFiles()
    {
        foreach (var content in new[] { "", "{}", "oops", "[]", """{"providers":[]}""", """{"providers":{},"models":[]}""",
                     """{"providers":[{"name":"other"}]}""", """{"models":{"other":{}}}""",
                     """{"providers":null}""", """{"unknown":{}}""", """{"$schema":"https://example.com/schema","version":1}""" })
        {
            var env = EnvironmentFor(content);
            var error = Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.GuardRegistry(env, directory));
            Assert.Contains("precedence", error.Message);
            Assert.Contains("COPILOT_PROVIDERS_CONFIG", error.Message);
            Assert.Contains("stricter", error.Message);
            Assert.Equal(content, File.ReadAllText(Path.Combine(directory, "providers.json")));
        }
    }

    [Fact]
    public void Registry_RejectsEveryExplicitOverrideEvenMissingOrEmpty()
    {
        var missing = new Dictionary<string, string?> { ["COPILOT_PROVIDERS_CONFIG"] = Path.Combine(directory, "missing") };
        Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.GuardRegistry(missing, directory));
        missing["COPILOT_PROVIDERS_CONFIG"] = directory;
        Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.GuardRegistry(missing, directory));
        missing["COPILOT_PROVIDERS_CONFIG"] = "";
        Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.GuardRegistry(missing, directory));
    }

    [Fact]
    public void Registry_AllowsOnlyMissingDefaultAndHonorsCopilotHome()
    {
        EnterpriseAuth.GuardRegistry(new Dictionary<string, string?>(), directory);
        var home = Path.Combine(directory, "custom");
        Directory.CreateDirectory(home);
        var env = new Dictionary<string, string?> { ["COPILOT_HOME"] = home };
        EnterpriseAuth.GuardRegistry(env, directory);
        File.WriteAllText(Path.Combine(home, "providers.json"), """{"providers":["blocked"]}""");
        Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.GuardRegistry(env, directory));
        File.Delete(Path.Combine(home, "providers.json"));
        Directory.CreateDirectory(Path.Combine(home, "providers.json"));
        Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.GuardRegistry(env, directory));
    }

    [Fact]
    public async Task Registry_GuardRunsBeforeAnyAzureOrHttp()
    {
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => CopilotX.Program.SetEnvironmentForProfile(Profile(),
            inherited: EnvironmentFor("""{"providers":["other"]}"""),
            runAzure: _ => { calls++; throw new Exception(); }));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Environment_IsChildOnlyClearsAllInheritedProviderState()
    {
        var env = EnvironmentFor();
        env["COPILOT_PROVIDER_API_KEY"] = "old";
        env["COPILOT_PROVIDER_BEARER_TOKEN"] = "old-bearer";
        env["COPILOT_PROVIDER_API_KEY_COMMAND"] = "command";
        env["COPILOT_PROVIDER_UNKNOWN_FUTURE"] = "unknown";
        env["COPILOT_MODEL"] = "stale";
        var before = EnterpriseAuth.CopyEnvironment();
        var profile = Profile();
        var result = await CopilotX.Program.SetEnvironmentForProfile(profile, inherited: env,
            runAzure: Azure(new(), Account(), new(0, TokenJson())), now: () => Now);
        Assert.Equal(Secret, result.Environment["COPILOT_PROVIDER_BEARER_TOKEN"]);
        Assert.Equal("logical-model", result.Environment["COPILOT_PROVIDER_MODEL_ID"]);
        Assert.Equal("production", result.Environment["COPILOT_PROVIDER_WIRE_MODEL"]);
        Assert.Equal("production", result.Environment["COPILOT_MODEL"]);
        Assert.Equal("azure", result.Environment["COPILOT_PROVIDER_TYPE"]);
        Assert.False(result.Environment.ContainsKey("COPILOT_PROVIDER_API_KEY"));
        Assert.False(result.Environment.ContainsKey("COPILOT_PROVIDER_API_KEY_COMMAND"));
        Assert.False(result.Environment.ContainsKey("COPILOT_PROVIDER_UNKNOWN_FUTURE"));
        Assert.False(result.UsedAzureCliToken);
        Assert.Equal("old", env["COPILOT_PROVIDER_API_KEY"]);
        Assert.Equal(before, EnterpriseAuth.CopyEnvironment());
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(profile));
    }

    [Fact]
    public async Task Environment_EntraPreservesExplicitProviderType()
    {
        var profile = Profile();
        profile.ProviderType = "openai";
        var result = await CopilotX.Program.SetEnvironmentForProfile(profile, inherited: EnvironmentFor(),
            runAzure: Azure(new(), Account(), new(0, TokenJson())), now: () => Now);
        Assert.Equal("openai", result.Environment["COPILOT_PROVIDER_TYPE"]);
    }

    [Fact]
    public async Task Environment_DefaultClearsSelectedAndUnknownProviderVariables()
    {
        var inherited = new Dictionary<string, string?> { ["COPILOT_PROVIDER_BEARER_TOKEN"] = "old", ["COPILOT_PROVIDER_FUTURE"] = "old", ["COPILOT_MODEL"] = "old", ["PATH"] = "keep" };
        var result = await CopilotX.Program.SetEnvironmentForProfile(new Profile(), inherited: inherited);
        Assert.Single(result.Environment);
        Assert.Equal("keep", result.Environment["PATH"]);
        Assert.Equal(4, inherited.Count);
    }

    [Fact]
    public async Task Environment_ApiKeysTakePrecedenceAndClearBearerAndCommand()
    {
        foreach (var explicitAuth in new[] { false, true })
        {
            var profile = new Profile { Type = "byok", BaseUrl = "https://example.openai.azure.com", Model = "m", ApiKeyEnv = "KEY", AzureCliToken = "on" };
            if (explicitAuth) profile.Authentication = new() { Type = "apiKey" };
            var result = await CopilotX.Program.SetEnvironmentForProfile(profile, inherited: new Dictionary<string, string?>
            {
                ["KEY"] = "configured-key", ["COPILOT_PROVIDER_BEARER_TOKEN"] = "stale",
                ["COPILOT_PROVIDER_API_KEY_COMMAND"] = "command"
            }, legacyToken: _ => throw new Exception("Must not acquire token"));
            Assert.Equal("configured-key", result.Environment["COPILOT_PROVIDER_API_KEY"]);
            Assert.False(result.Environment.ContainsKey("COPILOT_PROVIDER_BEARER_TOKEN"));
            Assert.False(result.Environment.ContainsKey("COPILOT_PROVIDER_API_KEY_COMMAND"));
        }
    }

    [Fact]
    public async Task Environment_LegacyScopeAndDeploymentMappingRemain()
    {
        var profile = new Profile { Type = "byok", Model = "logical", BaseUrl = "https://example.openai.azure.com/openai/deployments/url-deployment", TokenScope = "legacy-scope" };
        var result = await CopilotX.Program.SetEnvironmentForProfile(profile, inherited: new Dictionary<string, string?>(),
            legacyToken: p => { Assert.Equal("legacy-scope", p.TokenScope); return Task.FromResult("legacy-token"); });
        Assert.True(result.UsedAzureCliToken);
        Assert.Equal("url-deployment", result.Environment["COPILOT_MODEL"]);
        Assert.Equal("logical", result.Environment["COPILOT_PROVIDER_MODEL_ID"]);
    }

    [Fact]
    public void Launch_StandaloneEntraReceivesCopiedEnvironmentAndNeverRetries()
    {
        var profile = Profile();
        var env = new Dictionary<string, string?> { ["COPILOT_PROVIDER_BEARER_TOKEN"] = Secret, ["PATH"] = "keep" };
        var args = new[] { "-p", "create file" };
        var info = EnterpriseAuth.ChildStartInfo(profile, args, env, false);
        Assert.Equal("copilot", info.FileName);
        Assert.Equal(args, info.ArgumentList);
        Assert.Equal(Secret, info.Environment["COPILOT_PROVIDER_BEARER_TOKEN"]);
        info.Environment["PATH"] = "child";
        Assert.Equal("keep", env["PATH"]);
        Assert.False(CopilotX.Program.ShouldRetry(profile, 1, true, "401 expired token"));
        var legacy = new Profile();
        Assert.True(CopilotX.Program.ShouldRetry(legacy, 1, true, "401 expired token"));
        var legacyInfo = EnterpriseAuth.ChildStartInfo(legacy, args, env, true);
        Assert.Equal("gh", legacyInfo.FileName);
        Assert.Equal(new[] { "copilot", "--", "-p", "create file" }, legacyInfo.ArgumentList);
        Assert.False(legacyInfo.RedirectStandardOutput);
    }

    [Fact]
    public void Preflight_RequiresExplicitV1SurfaceRatherThanRewritingEndpoints()
    {
        foreach (var endpoint in new[] { "https://example.com/openai/v1", "https://example.com/openai/v1/" })
        {
            var profile = Profile(); profile.BaseUrl = endpoint;
            Assert.Equal("https://example.com/openai/v1/chat/completions", EnterpriseAuth.PreflightEndpoint(profile).ToString());
        }
        foreach (var endpoint in new[] { "https://example.com", "https://example.com/", "https://example.com/openai/deployments/prod", "https://example.com/models" })
        {
            var profile = Profile(); profile.BaseUrl = endpoint;
            Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.PreflightEndpoint(profile));
            profile.Authentication!.Preflight = true;
            Assert.Throws<InvalidOperationException>(() => EnterpriseAuth.Validate(profile));
        }
    }

    sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }

    [Fact]
    public async Task Preflight_UsesMinimalBilledRequestAndWireModel()
    {
        var count = 0;
        using var client = new HttpClient(new Handler(async request =>
        {
            count++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal(Secret, request.Headers.Authorization.Parameter);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("production", body.RootElement.GetProperty("model").GetString());
            Assert.Equal(1, body.RootElement.GetProperty("max_completion_tokens").GetInt32());
            Assert.False(body.RootElement.GetProperty("stream").GetBoolean());
            Assert.Equal(1, body.RootElement.GetProperty("messages").GetArrayLength());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Secret) };
        }));
        await EnterpriseAuth.Preflight(Profile(), Secret, client);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Preflight_DoesNotExposeErrorBodyAndDoesNotReplay()
    {
        foreach (var status in new[] { 401, 403, 429, 302, 500 })
        {
            var count = 0;
            using var client = new HttpClient(new Handler(_ =>
            {
                count++;
                return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(Secret) });
            }));
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => EnterpriseAuth.Preflight(Profile(), Secret, client));
            Assert.Contains(status.ToString(), error.Message);
            Assert.DoesNotContain(Secret, error.Message);
            Assert.Equal(1, count);
            if (status == 403) Assert.Contains("network", error.Message);
        }
    }

    [Fact]
    public async Task Preflight_IsOptInAndFailurePreventsLaunchPreparation()
    {
        var count = 0;
        using var client = new HttpClient(new Handler(_ =>
        {
            count++;
            throw new HttpRequestException(Secret);
        }));
        var profile = Profile();
        await CopilotX.Program.SetEnvironmentForProfile(profile, inherited: EnvironmentFor(),
            runAzure: Azure(new(), Account(), new(0, TokenJson())), now: () => Now, preflightClient: client);
        Assert.Equal(0, count);
        profile.Authentication!.Preflight = true;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => CopilotX.Program.SetEnvironmentForProfile(profile,
            inherited: EnvironmentFor(), runAzure: Azure(new(), Account(), new(0, TokenJson())), now: () => Now, preflightClient: client));
        Assert.Equal(1, count);
        Assert.DoesNotContain(Secret, error.Message);
    }
}
