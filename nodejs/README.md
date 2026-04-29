# CopilotX - Node.js CLI

A lightweight Node.js CLI wrapper around GitHub Copilot CLI for easy model switching between default Copilot and custom BYOK models.

## Installation

```bash
cd nodejs
npm install
npm link
```

This will make the `copilotx` command available globally.

## Prerequisites

- Node.js 14 or higher
- GitHub Copilot CLI installed: `gh extension install github/gh-copilot`

## Usage

### List available profiles

```bash
copilotx list
```

Shows all configured profiles with details. The last used profile is marked with `*`.

### Use a specific profile

```bash
copilotx use <profile-name>
```

Switch to a profile and run GitHub Copilot CLI with that configuration.

You can pass additional arguments to `gh copilot`:

```bash
copilotx use azure-gpt suggest "how to list files"
copilotx use ollama-local explain "what is this code doing"
```

### Use the last used profile

```bash
copilotx last
```

Quick access to your most recently used profile.

### Use default Copilot

```bash
copilotx default
```

Switch back to the default GitHub Copilot (no BYOK).

### Add a new profile

```bash
copilotx add
```

Interactive wizard to add or update a profile.

### Import profiles from Foundry deployments

```bash
# Scan all applicable accounts and prompt per deployment
copilotx import-foundry --mode each

# Add all deployments without prompt
copilotx import-foundry --all

# Target one account/resource group
copilotx import-foundry --account myfoundry --resource-group my-rg --all
```

## Configuration

Profiles are stored in an active config file under `~/.copilotx/`.

Config scope behavior:
- `COPILOTX_CONFIG_SCOPE=auto` (default): use Azure user-scoped config when `az account show` is available, otherwise global config
- `COPILOTX_CONFIG_SCOPE=azure-user`: always use Azure user-scoped config
- `COPILOTX_CONFIG_SCOPE=global`: always use `~/.copilotx/config.json`

In Azure user-scoped mode, file name format is:
- `~/.copilotx/config.<tenantId>__<userName>.json`

Use `copilotx list` to see which config file is currently active.

### Example Configuration

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
    }
  ],
  "lastUsed": "azure-gpt"
}
```

### Profile Types

#### `copilot` (Default GitHub Copilot)

```json
{
  "name": "default",
  "type": "copilot",
  "model": "auto"
}
```

Uses the standard GitHub Copilot service without custom configuration.

#### `byok` (Bring Your Own Key)

```json
{
  "name": "custom-model",
  "type": "byok",
  "baseUrl": "https://api.openai.com/v1",
  "apiKeyEnv": "OPENAI_API_KEY",
  "model": "gpt-4",
  "providerType": "openai"
}
```

Fields:
- `baseUrl`: API endpoint URL
- `model`: Model name
- `apiKeyEnv`: Environment variable containing the API key
- `apiKey`: Direct API key (alternative to `apiKeyEnv`, less secure)
- `providerType`: Optional provider type
- `azureCliToken`: Optional token mode (`auto`, `on`, `off`)
- `tokenScope`: Optional Azure token scope

#### `proxy` (Proxy Configuration)

Same as `byok`, useful for enterprise scenarios with API Management or token-based auth.

## How It Works

The tool sets environment variables before launching `gh copilot`:

**Default Copilot Mode:**
```bash
unset COPILOT_PROVIDER_BASE_URL
unset COPILOT_PROVIDER_API_KEY
unset COPILOT_MODEL
gh copilot
```

**BYOK Mode:**
```bash
export COPILOT_PROVIDER_BASE_URL=<url>
export COPILOT_PROVIDER_API_KEY=<key>
export COPILOT_MODEL=<model>
gh copilot
```

If `azureCliToken` is enabled (or `auto` detects Azure profile with no API key), CopilotX runs:

```bash
az account get-access-token --scope https://cognitiveservices.azure.com/.default --query accessToken -o tsv
```

Then it sets the returned token as `COPILOT_PROVIDER_BEARER_TOKEN`. In token mode, `COPILOT_PROVIDER_API_KEY` is cleared to avoid auth-mode ambiguity.

Retry behavior:
- If `gh copilot` fails with token/auth-related errors, CopilotX refreshes the token and retries once.

## Enterprise Scenarios

### Azure OpenAI with API Key

```json
{
  "name": "azure-enterprise",
  "type": "byok",
  "baseUrl": "https://your-resource.openai.azure.com/openai/deployments/your-deployment",
  "apiKeyEnv": "AZURE_OPENAI_KEY",
  "model": "gpt-4"
}
```

### Azure OpenAI with RBAC (via Proxy)

For RBAC-only scenarios, use a proxy layer (APIM, LiteLLM) that handles token authentication:

```json
{
  "name": "azure-rbac",
  "type": "proxy",
  "baseUrl": "https://your-apim.azure-api.net",
  "apiKeyEnv": "APIM_SUBSCRIPTION_KEY",
  "model": "gpt-4"
}
```

The proxy handles:
- Token acquisition (Microsoft Entra ID)
- Token refresh
- RBAC authentication

### Azure OpenAI with RBAC (API Keys Disabled, Local Wrapper)

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

Requirements:
- Azure CLI installed
- Logged in: `az login`

## Import From Foundry

`import-foundry` uses Azure CLI to discover deployments and create/update profiles in `~/.copilotx/config.json`.

Imported profiles are created as:
- `type: "byok"`
- `providerType: "azure"`
- `azureCliToken: "auto"`
- `tokenScope: "https://cognitiveservices.azure.com/.default"`

### Ollama Local

```json
{
  "name": "ollama",
  "type": "byok",
  "baseUrl": "http://localhost:11434",
  "model": "llama3"
}
```

## Examples

```bash
# Add Azure OpenAI profile
copilotx add
# Follow prompts...

# List all profiles
copilotx list

# Use Azure profile
copilotx use azure-gpt suggest "create a function to sort an array"

# Switch back to default
copilotx default suggest "explain this code"

# Use last profile
copilotx last
```

## Troubleshooting

### "Profile not found"
Run `copilotx list` to see available profiles or `copilotx add` to create a new one.

### "Error executing gh copilot"
Ensure GitHub Copilot CLI is installed:
```bash
gh extension install github/gh-copilot
```

### API key not found
If using `apiKeyEnv`, ensure the environment variable is set:
```bash
export AZURE_OPENAI_KEY="your-key-here"
```

## Testing

Run unit tests with Node's built-in test runner:

```bash
npm test
```

Current tests cover deterministic config path resolution and identity-scoped config behavior.
They also cover file persistence in isolated temp config directories, profile upsert behavior, and last-used persistence.

## License

MIT
