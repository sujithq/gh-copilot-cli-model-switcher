# GitHub Copilot CLI Model Switcher

[![gh-copilot-byok](https://img.shields.io/nuget/v/gh-copilot-byok.svg)](https://www.nuget.org/packages/gh-copilot-byok)
[![gh-copilot-byok](https://img.shields.io/nuget/dt/gh-copilot-byok)](https://www.nuget.org/packages/gh-copilot-byok)
[![dotnet tool](https://img.shields.io/badge/.NET-Global%20Tool-blue)]()

A lightweight CLI wrapper ("minitool") around GitHub Copilot CLI that enables easy switching between default Copilot models and custom BYOK (Bring Your Own Key) models.

## 🎯 Overview

gh-copilot-byok allows you to:

- ✅ Easily switch between default Copilot and custom models
- ✅ Persist model configurations locally
- ✅ Reuse previously selected configurations
- ✅ Support enterprise scenarios (Azure OpenAI / Foundry)
- ✅ Connect to any OpenAI-compatible endpoint

## 🚀 Available Implementations

This repository provides two implementations of the same tool:

### 1. Node.js CLI (yargs-based)
- **Location:** `/nodejs`
- **Framework:** Node.js with yargs
- **Ideal for:** Cross-platform environments, npm users
- **Documentation:** [nodejs/README.md](nodejs/README.md)

### 2. .NET Tool (Spectre.Console-based)
- **Location:** `/dotnet/CopilotX`
- **Framework:** .NET 10+ with Spectre.Console
- **Ideal for:** .NET developers, enterprise environments
- **Features:** Beautiful colored CLI with interactive prompts
- **Documentation:** [dotnet/CopilotX/README.md](dotnet/CopilotX/README.md)

Both implementations share the same configuration format and provide identical functionality.

## 📦 Quick Start

### Node.js Version

```bash
cd nodejs
npm install
npm link
gh-copilot-byok list
```

## ✅ Testing

### Run all tests (one command)

```powershell
./run-tests.ps1
```

### Node.js unit tests

```bash
cd nodejs
npm test
```

Uses Node's built-in test runner (`node --test`).

### .NET unit tests (xUnit)

```bash
cd dotnet/CopilotX.Tests
dotnet run
```

The `.NET` test project uses xUnit with a lightweight console test runner.

### .NET Version

```bash
cd dotnet/CopilotX
dotnet pack
dotnet tool install --global --add-source ./nupkg gh-copilot-byok
gh-copilot-byok list
```

### Install from NuGet.org (recommended)

```bash
dotnet tool install --global gh-copilot-byok
```

Update an existing installation:

```bash
dotnet tool update --global gh-copilot-byok
```

### Install from GitHub Packages (alternative — requires authentication)

```bash
dotnet tool install --global gh-copilot-byok --add-source "https://nuget.pkg.github.com/sujithq/index.json"
```

Update an existing installation:

```bash
dotnet tool update --global gh-copilot-byok --add-source "https://nuget.pkg.github.com/sujithq/index.json"
```

## 🎮 Usage

All commands are identical across both implementations:

```bash
# List available profiles (interactive menu: select #, then press Enter to use)
gh-copilot-byok list
gh-copilot-byok list

# Use a specific profile
gh-copilot-byok use azure-gpt

# Add a new profile
gh-copilot-byok add

# Use last profile
gh-copilot-byok last

# Use default Copilot
gh-copilot-byok default

# Import profiles from Foundry / Azure OpenAI / Azure AI Services deployments
gh-copilot-byok import-foundry --mode each

# Import all discovered deployments from all applicable accounts
gh-copilot-byok import-foundry --all

# Import from one account/resource group
gh-copilot-byok import-foundry --account myfoundry --resource-group my-rg --all
```

## 🚢 Release Readiness

This repository now includes CI and release automation:

- `.github/workflows/ci.yml`
  - Runs Node tests
  - Runs .NET build/tests
  - Validates .NET packing
- `.github/workflows/release-dotnet-tool.yml`
  - Triggered when a GitHub release is published
  - Builds/tests/packs
  - Publishes to GitHub Packages NuGet feed
  - Uploads package artifacts to the published GitHub Release
- `.github/workflows/release-please.yml`
  - Generates semantic version bumps and release notes via Release Please
  - Opens/updates release PRs from conventional commits

Release guide: [RELEASING.md](RELEASING.md)

### Conventional Commits (SemVer)

Release Please determines version bumps from commit messages:

- `fix:` -> patch bump (x.y.Z)
- `feat:` -> minor bump (x.Y.0)
- `feat!:` or `BREAKING CHANGE:` -> major bump (X.0.0)
- `docs:` -> no release by default

Examples:

- `fix(node): handle null mcp list`
- `feat(dotnet): add mcp-compat command`
- `feat!: drop legacy config format`

If a GitHub Release tag already exists and publish needs to be rerun manually, use the `Release Please` workflow's optional `release_tag` input.

## 🧱 Core Concepts

### BYOK (Bring Your Own Key)

GitHub Copilot CLI can connect to external models via environment variables:
- `COPILOT_PROVIDER_BASE_URL`
- `COPILOT_PROVIDER_API_KEY`
- `COPILOT_PROVIDER_BEARER_TOKEN`
- `COPILOT_MODEL`
- `COPILOT_PROVIDER_TYPE`
- `COPILOT_PROVIDER_MAX_OUTPUT_TOKENS`
- `COPILOT_PROVIDER_MAX_PROMPT_TOKENS`

gh-copilot-byok can now source Azure CLI access tokens when API keys are disabled. In token mode, it sets `COPILOT_PROVIDER_BEARER_TOKEN` and clears `COPILOT_PROVIDER_API_KEY` to avoid auth-mode ambiguity.

For Azure BYOK profiles, gh-copilot-byok also enables an MCP compatibility mode by default to avoid provider tool-count limits (e.g. `tools array too long`). It launches `gh copilot` with MCP-disable flags for common high-volume servers. Set `CBMS_DISABLE_MCP_COMPAT=off` to opt out.

This allows connecting to:
- OpenAI
- Azure OpenAI
- Ollama
- Any OpenAI-compatible endpoint

### OpenAI-Compatible Endpoint

Copilot CLI requires:
- API compatible with `/v1/chat/completions`
- Tool/function calling support
- Streaming support

### Model Requirements

Any model used must support:
- Tool calling
- Streaming responses

## 📝 Configuration

Profiles are stored in an active config file under `~/.copilot-byok-model-switcher/`.

Config scope behavior:
- `COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE=auto` (default): use Azure user-scoped config when `az account show` is available, otherwise global config
- `COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE=azure-user`: always use Azure user-scoped config
- `COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE=global`: always use `~/.copilot-byok-model-switcher/config.json`

In Azure user-scoped mode, file name format is:
- `~/.copilot-byok-model-switcher/config.<tenantId>__<userName>.json`

Use `gh-copilot-byok list` to see which config file is currently active.

Example JSON content:

```json
{
  "profiles": [
    {
      "name": "default",
      "type": "copilot",
      "model": "auto"
    },
    {
      "name": "azure-gpt",
      "type": "byok",
      "baseUrl": "https://xxx.openai.azure.com/openai/deployments/gpt",
      "apiKeyEnv": "AZURE_OPENAI_KEY",
      "model": "gpt-4"
    },
    {
      "name": "ollama-local",
      "type": "byok",
      "baseUrl": "http://localhost:11434",
      "model": "llama3"
    },
    {
      "name": "openai-token-metered",
      "type": "byok",
      "baseUrl": "https://api.openai.com/v1",
      "apiKeyEnv": "OPENAI_API_KEY",
      "model": "gpt-5",
      "maxOutputTokens": 4096,
      "maxPromptTokens": 64000
    }
  ],
  "lastUsed": "azure-gpt"
}
```

### Profile Types

#### 1. `copilot` - Default GitHub Copilot

```json
{
  "name": "default",
  "type": "copilot",
  "model": "auto"
}
```

#### 2. `byok` - Bring Your Own Key

```json
{
  "name": "custom-model",
  "type": "byok",
  "baseUrl": "https://api.openai.com/v1",
  "apiKeyEnv": "OPENAI_API_KEY",
  "model": "gpt-4",
  "maxOutputTokens": 4096,
  "maxPromptTokens": 64000
}
```

Optional auth fields for `byok` and `proxy`:

```json
{
  "azureCliToken": "auto",
  "tokenScope": "https://cognitiveservices.azure.com/.default",
  "maxOutputTokens": 4096,
  "maxPromptTokens": 64000
}
```

- `azureCliToken`: `auto` (default), `on`, or `off`
- `tokenScope`: Azure token scope for `az account get-access-token`
- `maxOutputTokens`: Optional cap for generated output tokens
- `maxPromptTokens`: Optional cap for prompt/context tokens sent to the provider
- `maxTokens`: Legacy alias for `maxOutputTokens`; still read for compatibility but new configs should use `maxOutputTokens`

#### 3. `proxy` - Proxy Configuration

```json
{
  "name": "enterprise",
  "type": "proxy",
  "baseUrl": "https://your-apim.azure-api.net",
  "apiKeyEnv": "APIM_KEY",
  "model": "gpt-4"
}
```

## 🏢 Enterprise Scenarios

### Azure OpenAI with API Key

```json
{
  "name": "azure-enterprise",
  "type": "byok",
  "baseUrl": "https://your-resource.openai.azure.com/openai/deployments/your-deployment",
  "apiKeyEnv": "AZURE_OPENAI_KEY",
  "model": "gpt-4",
  "maxOutputTokens": 4096
}
```

### Azure OpenAI with RBAC (via Proxy)

For RBAC-only scenarios where API keys are disabled:

```
Copilot CLI → Proxy (APIM/LiteLLM) → Azure OpenAI (RBAC)
```

The proxy handles:
- Token acquisition (Microsoft Entra ID)
- Token refresh
- RBAC authentication

### Azure OpenAI with RBAC (No API Keys, Local Wrapper)

For environments where API keys are disabled, gh-copilot-byok can acquire an Entra token directly using Azure CLI:

```json
{
  "name": "azure-rbac-local",
  "type": "byok",
  "baseUrl": "https://your-resource.openai.azure.com/openai/deployments/your-deployment",
  "model": "gpt-4",
  "providerType": "azure",
  "azureCliToken": "auto",
  "tokenScope": "https://cognitiveservices.azure.com/.default"
}
```

Behavior:
- Runs `az account get-access-token` before launching `gh copilot`
- Auto-detects Azure profiles in `auto` mode when no API key is configured
- Retries once after refreshing token if auth errors indicate token expiry/failure

Configuration:

```json
{
  "name": "azure-rbac",
  "type": "proxy",
  "baseUrl": "https://your-apim.azure-api.net",
  "apiKeyEnv": "APIM_SUBSCRIPTION_KEY",
  "model": "gpt-4"
}
```

### Ollama Local

```json
{
  "name": "ollama",
  "type": "byok",
  "baseUrl": "http://localhost:11434",
  "model": "llama3"
}
```

## 🔄 How It Works

### Default Copilot Mode

```bash
unset COPILOT_PROVIDER_BASE_URL
unset COPILOT_PROVIDER_API_KEY
unset COPILOT_MODEL
gh copilot
```

### BYOK Mode

```bash
export COPILOT_PROVIDER_BASE_URL=<url>
export COPILOT_PROVIDER_API_KEY=<key>
export COPILOT_MODEL=<model>
gh copilot
```

### BYOK Mode (Azure CLI Token)

```bash
export COPILOT_PROVIDER_BASE_URL=<azure-openai-deployment-url>
export COPILOT_PROVIDER_BEARER_TOKEN=<token from az account get-access-token>
export COPILOT_MODEL=<model>
gh copilot
```

gh-copilot-byok handles this switching automatically based on the selected profile.

## 🧭 Import From Foundry

Use `import-foundry` to discover deployed models and generate `byok` profiles automatically.

Each imported profile is configured with:
- `providerType: "azure"`
- `azureCliToken: "auto"`
- `tokenScope: "https://cognitiveservices.azure.com/.default"`

Examples:

```bash
# Scan all accounts and prompt per deployment
gh-copilot-byok import-foundry --mode each

# Scan all accounts and add all discovered deployments
gh-copilot-byok import-foundry --all

# Target one account/resource group
gh-copilot-byok import-foundry --account myfoundry --resource-group my-rg --mode each

# Scope by subscription
gh-copilot-byok import-foundry --subscription <subscription-id> --all
```

## 📚 Examples

### Adding a Profile

#### Node.js Version
```bash
gh-copilot-byok add
# Follow the interactive prompts
```

#### .NET Version (with Spectre.Console)
```bash
gh-copilot-byok add
# Beautiful interactive prompts with selection menus
# Secure password input for API keys
```

### Using Profiles

```bash
# List all profiles
gh-copilot-byok list

# Use Azure profile with Copilot prompt mode
gh-copilot-byok use azure-gpt -p "create a function to sort an array"

# Use Ollama for local inference
gh-copilot-byok use ollama-local -p "what is this code"

# Quick access to last used profile
gh-copilot-byok last -p "how to debug this"

# Switch back to default
gh-copilot-byok default
```

## 🛠️ Architecture

### Components

1. **Config Manager**: Handles loading, saving, and managing profiles
2. **Profile Switcher**: Sets environment variables based on profile
3. **CLI Interface**: User-facing commands and interactions
4. **Copilot Launcher**: Executes `gh copilot` with configured environment

### Flow

```
User Command
    ↓
Parse Arguments
    ↓
Load Profile Config
    ↓
Set Environment Variables
    ↓
Execute gh copilot
    ↓
Return Result
```

## 🔐 Security Best Practices

1. **Use Environment Variables**: Store API keys in environment variables, not directly in config
2. **API Key Security**: Use `apiKeyEnv` instead of `apiKey` in profiles
3. **File Permissions**: Ensure `~/.copilot-byok-model-switcher/config.json` has appropriate permissions
4. **Enterprise RBAC**: Use proxy layer for token-based authentication
5. **Identity Separation**: Azure user-scoped config keeps profiles separate when switching users with `az login`

## 🚀 Future Enhancements

Potential features for future versions:

- Interactive picker (fzf integration)
- Per-repository profiles
- Git branch-specific configurations
- Auto fallback models
- Model routing based on cost/latency
- Profile templates
- Import/export profiles
- Shell completion (bash, zsh, fish)

## 📄 Prerequisites

### Common Requirements
- GitHub Copilot CLI: `gh extension install github/gh-copilot`

### Node.js Version
- Node.js 14 or higher
- npm or yarn

### .NET Version
- .NET 10 SDK or higher (compatible with .NET 6+)

## 🤝 Contributing

Contributions are welcome! Both implementations should maintain feature parity.

## 📜 License

MIT

## 🔗 Related Resources

- [GitHub Copilot CLI Documentation](https://docs.github.com/en/copilot/github-copilot-in-the-cli)
- [OpenAI API Reference](https://platform.openai.com/docs/api-reference)
- [Azure OpenAI Service](https://azure.microsoft.com/en-us/products/ai-services/openai-service)
- [Ollama](https://ollama.ai/)
- [Spectre.Console](https://spectreconsole.net/)
- [yargs](https://yargs.js.org/)
