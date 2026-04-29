namespace CopilotX;

public static class Env
{
    private static readonly string[] Keys =
    [
        "COPILOT_PROVIDER_BASE_URL",
        "COPILOT_PROVIDER_API_KEY",
        "COPILOT_PROVIDER_TYPE",
        "COPILOT_MODEL",
    ];

    public static Dictionary<string, string> BuildForProfile(Profile profile)
    {
        var env = Environment.GetEnvironmentVariables();
        var next = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in env.Keys)
        {
            if (key is string s && env[key] is string v)
                next[s] = v;
        }

        foreach (var k in Keys) next.Remove(k);

        if (profile.Type == "copilot") return next;

        if (profile.Type != "byok" && profile.Type != "proxy")
            throw new InvalidOperationException($"Unknown profile type: {profile.Type}");

        if (string.IsNullOrWhiteSpace(profile.BaseUrl))
            throw new InvalidOperationException("Profile is missing baseUrl");

        next["COPILOT_PROVIDER_BASE_URL"] = profile.BaseUrl;
        if (!string.IsNullOrWhiteSpace(profile.ProviderType))
            next["COPILOT_PROVIDER_TYPE"] = profile.ProviderType;
        if (!string.IsNullOrWhiteSpace(profile.Model))
            next["COPILOT_MODEL"] = profile.Model;

        if (!string.IsNullOrWhiteSpace(profile.ApiKey))
        {
            next["COPILOT_PROVIDER_API_KEY"] = profile.ApiKey;
        }
        else if (!string.IsNullOrWhiteSpace(profile.ApiKeyEnv))
        {
            var val = Environment.GetEnvironmentVariable(profile.ApiKeyEnv);
            if (string.IsNullOrWhiteSpace(val))
                throw new InvalidOperationException($"Environment variable {profile.ApiKeyEnv} is not set");
            next["COPILOT_PROVIDER_API_KEY"] = val;
        }

        return next;
    }
}

