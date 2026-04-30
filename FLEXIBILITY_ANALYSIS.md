# Tool Flexibility Analysis: Model Hosting & Authentication Support

## TL;DR

**Highly flexible.** The tool can work with **any model provider** that:
1. Exposes an OpenAI-compatible API endpoint
2. Uses API keys or bearer tokens for authentication

**Supports**:
- ✅ Microsoft Foundry (API key or bearer token)
- ✅ Azure OpenAI BYOK (API key or Azure CLI token)
- ✅ OpenAI direct API
- ✅ Ollama/vLLM (local or remote)
- ✅ Any OpenAI-compatible API provider (LiteLLM, Together.ai, etc.)

---

## 1. Architecture: Provider-Agnostic Design

The tool is **completely decoupled from specific providers**. It acts as a **profile switcher and environment variable injector** for the GitHub Copilot CLI.

### Profile Configuration Structure

```json
{
  "name": "profile-name",
  "type": "copilot" | "byok" | "proxy",
  "baseUrl": "https://api-endpoint",
  "model": "model-name",
  "apiKey": "key-value",
  "apiKeyEnv": "ENV_VAR_NAME",
  "providerType": "azure" | "custom",
  "azureCliToken": "on" | "off" | "auto",
  "tokenScope": "https://scope.uri",
  "mcpCompatServers": ["server1", "server2"]
}
```

**Key insight**: Only `baseUrl`, `model`, and `apiKey` (or `apiKeyEnv`) are required. Everything else is optional.

---

## 2. Authentication Methods: Fully Supported

### A. API Key Authentication

**Direct key in config** (not recommended for production):
```json
{
  "name": "ollama-local",
  "type": "byok",
  "baseUrl": "http://localhost:11434/v1",
  "model": "llama3",
  "apiKey": "sk-..."
}
```

**Environment variable reference** (recommended):
```json
{
  "name": "openai-api",
  "type": "byok",
  "baseUrl": "https://api.openai.com/v1",
  "model": "gpt-4",
  "apiKeyEnv": "OPENAI_API_KEY"
}
```

**How it works** (Program.cs lines 897-920):
```csharp
string? resolvedApiKey = null;

if (!string.IsNullOrEmpty(profile.ApiKeyEnv))
{
    var apiKey = Environment.GetEnvironmentVariable(profile.ApiKeyEnv);
    if (!string.IsNullOrEmpty(apiKey))
        resolvedApiKey = apiKey;
}
else if (!string.IsNullOrEmpty(profile.ApiKey))
    resolvedApiKey = profile.ApiKey;

if (!string.IsNullOrEmpty(resolvedApiKey))
{
    Environment.SetEnvironmentVariable("COPILOT_PROVIDER_API_KEY", resolvedApiKey);
    Environment.SetEnvironmentVariable("COPILOT_PROVIDER_BEARER_TOKEN", null);
}
```

✅ **Works with**:
- Microsoft Foundry (API key mode)
- Azure OpenAI (API key)
- OpenAI (API key)
- Any OpenAI-compatible provider (API key)
- PAT-based systems (can be stored in `apiKeyEnv`)

---

### B. Bearer Token Authentication

**Azure CLI Token** (RBAC/Entra ID):
```json
{
  "name": "azure-rbac",
  "type": "byok",
  "baseUrl": "https://your-resource.openai.azure.com/openai/deployments/gpt-4",
  "model": "gpt-4",
  "providerType": "azure",
  "azureCliToken": "auto",
  "tokenScope": "https://cognitiveservices.azure.com/.default"
}
```

**How it works** (Program.cs lines 900-905):
```csharp
var useAzureCliToken = ShouldUseAzureCliToken(profile, !string.IsNullOrEmpty(resolvedApiKey));
if (useAzureCliToken)
{
    var token = await GetAzureCliToken(profile);
    Environment.SetEnvironmentVariable("COPILOT_PROVIDER_BEARER_TOKEN", token);
    Environment.SetEnvironmentVariable("COPILOT_PROVIDER_API_KEY", null);
}
```

**Token resolution logic** (Program.cs lines 771-790):
```csharp
static bool ShouldUseAzureCliToken(Profile profile, bool hasApiKey)
{
    var mode = (profile.AzureCliToken ?? "auto").ToLowerInvariant();

    if (mode == "on")
        return true;

    if (mode == "off")
        return false;

    // "auto" mode: use token if no API key AND Azure profile
    return !hasApiKey && IsAzureProfile(profile);
}
```

✅ **Works with**:
- Azure BYOK with RBAC (Entra ID tokens)
- Microsoft Foundry (if using bearer tokens)
- Any bearer token scheme (OAuth2, JWT, etc.)

---

### C. Multiple API Key Environments

The tool supports **environment variable fallback chains**. You can switch between different credentials by simply changing the referenced env var:

```json
[
  {
    "name": "openai-dev",
    "type": "byok",
    "baseUrl": "https://api.openai.com/v1",
    "model": "gpt-4",
    "apiKeyEnv": "OPENAI_DEV_KEY"
  },
  {
    "name": "openai-prod",
    "type": "byok",
    "baseUrl": "https://api.openai.com/v1",
    "model": "gpt-4",
    "apiKeyEnv": "OPENAI_PROD_KEY"
  }
]
```

---

## 3. Provider Type Flexibility

### Optional `providerType` Field

```csharp
if (!string.IsNullOrEmpty(profile.ProviderType))
{
    Environment.SetEnvironmentVariable("COPILOT_PROVIDER_TYPE", profile.ProviderType);
}
```

Currently used for Azure-specific optimizations:
- Detects Azure endpoints via `.openai.azure.com` domain OR explicit `providerType: "azure"`
- Enables **MCP compatibility mode** to avoid tool-count limits (Azure-specific issue)
- Extracts deployment name from URL for Azure

**Supported values**:
- `"azure"` (triggers Azure-specific logic)
- Any custom string (passed to `COPILOT_PROVIDER_TYPE` for gh copilot to use)
- Not set (provider-agnostic mode)

---

## 4. Real-World Use Cases

### ✅ Microsoft Foundry with API Key
```json
{
  "name": "foundry-gpt4",
  "type": "byok",
  "baseUrl": "https://your-foundry-endpoint.openai.azure.com/openai/deployments/gpt-4",
  "model": "gpt-4",
  "apiKeyEnv": "FOUNDRY_API_KEY"
}
```

### ✅ Microsoft Foundry with Bearer Token
```json
{
  "name": "foundry-rbac",
  "type": "byok",
  "baseUrl": "https://your-foundry-endpoint.openai.azure.com/openai/deployments/gpt-4",
  "model": "gpt-4",
  "providerType": "azure",
  "azureCliToken": "auto",
  "tokenScope": "https://cognitiveservices.azure.com/.default"
}
```

### ✅ Generic OpenAI-Compatible API (with PAT)
```json
{
  "name": "litellm-proxy",
  "type": "byok",
  "baseUrl": "http://localhost:8000/v1",
  "model": "gpt-4",
  "apiKeyEnv": "LITELLM_API_KEY"
}
```

### ✅ Private Ollama Instance
```json
{
  "name": "ollama-remote",
  "type": "byok",
  "baseUrl": "https://ollama.internal.company.com/v1",
  "model": "mistral",
  "apiKey": "optional-key-if-exposed-with-auth"
}
```

### ✅ vLLM with Auth
```json
{
  "name": "vllm-llama",
  "type": "byok",
  "baseUrl": "https://vllm.example.com/v1",
  "model": "meta-llama/Llama-2-70b",
  "apiKeyEnv": "VLLM_API_KEY"
}
```

### ✅ Multiple Azure Subscriptions with Different Tokens
```json
[
  {
    "name": "azure-sub1",
    "type": "byok",
    "baseUrl": "https://sub1.openai.azure.com/openai/deployments/gpt-4",
    "model": "gpt-4",
    "providerType": "azure",
    "azureCliToken": "auto",
    "tokenScope": "https://cognitiveservices.azure.com/.default"
  },
  {
    "name": "azure-sub2",
    "type": "byok",
    "baseUrl": "https://sub2.openai.azure.com/openai/deployments/gpt-4",
    "model": "gpt-4",
    "providerType": "azure",
    "azureCliToken": "auto",
    "tokenScope": "https://cognitiveservices.azure.com/.default"
  }
]
```

When you use a profile, Azure CLI token is automatically fetched for the subscription associated with the logged-in identity. The tool calls `az account show` to determine context, then `az account get-access-token` with the `tokenScope`.

---

## 5. Environment Variables Set by the Tool

For any profile (`byok` or `proxy` type), the tool injects:

| Env Var | Source | Example |
|---------|--------|---------|
| `COPILOT_PROVIDER_BASE_URL` | `profile.baseUrl` | `https://api.openai.com/v1` |
| `COPILOT_PROVIDER_API_KEY` | `profile.apiKey` or `ENV[profile.apiKeyEnv]` | `sk-...` |
| `COPILOT_PROVIDER_BEARER_TOKEN` | Azure CLI or custom token | `eyJ0...` |
| `COPILOT_MODEL` | `profile.model` or Azure deployment name | `gpt-4` |
| `COPILOT_PROVIDER_TYPE` | `profile.providerType` | `azure` |

**Key behaviors**:
- Either `COPILOT_PROVIDER_API_KEY` OR `COPILOT_PROVIDER_BEARER_TOKEN` is set, never both (lines 907-913)
- If Azure CLI token is used, `COPILOT_PROVIDER_API_KEY` is explicitly cleared
- `COPILOT_PROVIDER_TYPE` is only set if `providerType` is defined in the profile

---

## 6. Why This Design Works

### 1. **Delegation Model**
The tool doesn't validate provider compatibility—it delegates to `gh copilot`. The GitHub Copilot CLI handles the actual provider interaction.

### 2. **OpenAI-Compatible Standard**
Almost all modern AI providers support the OpenAI API format:
- Request format (same endpoint/model structure)
- Response format (same JSON schema)
- Authentication (API key or bearer token)

### 3. **No Hard-Coded Provider Logic**
Only Azure has special handling because:
- Azure requires deployment name (not model name) in `COPILOT_MODEL`
- Azure has tool-count limits triggering MCP compatibility mode
- Azure uses Entra ID for RBAC

Everything else is generic.

---

## 7. Limitations & Workarounds

### ❌ If a provider doesn't support OpenAI API format
**Example**: GraphQL-only API
- **Limitation**: Tool can't inject environment variables for custom protocols
- **Workaround**: Use a proxy layer (e.g., LiteLLM) that translates to OpenAI format

### ❌ If authentication requires custom headers (not API Key or Bearer Token)
**Example**: Custom `X-API-Key` header
- **Limitation**: Tool only supports `COPILOT_PROVIDER_API_KEY` and `COPILOT_PROVIDER_BEARER_TOKEN`
- **Workaround**: Proxy layer or wrapper that translates headers

### ⚠️ Token Refresh
- **Azure CLI tokens**: Auto-refreshed once on failure (line 544)
- **Other bearer tokens**: No auto-refresh (set once at startup)
- **API Keys**: Static, no refresh

---

## 8. Recommendations for Extensibility

If you want to add support for additional auth methods (e.g., mTLS, API Gateway keys):

1. **Add profile fields**:
   ```csharp
   [JsonPropertyName("mtlsCert")]
   public string? MtlsCertPath { get; set; }

   [JsonPropertyName("customHeaders")]
   public Dictionary<string, string>? CustomHeaders { get; set; }
   ```

2. **Expand `SetEnvironmentForProfile`** to handle new auth types:
   ```csharp
   if (!string.IsNullOrEmpty(profile.MtlsCertPath))
   {
       Environment.SetEnvironmentVariable("COPILOT_PROVIDER_CERT", profile.MtlsCertPath);
   }
   ```

3. **Ensure `gh copilot` recognizes the new env vars** (coordination with GitHub)

---

## Conclusion

**The tool is extremely flexible and can work with virtually any provider** that:
- Exposes an OpenAI-compatible REST API
- Uses API keys, bearer tokens, or Azure CLI for authentication

**Current support**:
- ✅ Microsoft Foundry (both API key and bearer token modes)
- ✅ Azure OpenAI (BYOK with API key or RBAC)
- ✅ OpenAI direct
- ✅ Ollama/vLLM
- ✅ Any OpenAI-compatible provider (LiteLLM, Together.ai, Replicate, etc.)

**To use a new provider**: Add a profile with `baseUrl`, `model`, and `apiKeyEnv` (or `apiKey`). Done.
