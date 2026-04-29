# CopilotX - .NET Tool

A lightweight .NET CLI tool wrapper around GitHub Copilot CLI for easy model switching between default Copilot and custom BYOK models.

Built with [Spectre.Console](https://spectreconsole.net/) for a beautiful CLI experience.

## Installation

### Install as a .NET Global Tool

```bash
cd dotnet/CopilotX
dotnet pack
dotnet tool install --global --add-source ./nupkg CopilotX
```

This will make the `copilotx` command available globally.

### Uninstall

```bash
dotnet tool uninstall --global CopilotX
```

## Prerequisites

- .NET 10 SDK or higher (compatible with .NET 6+)
- GitHub Copilot CLI installed: `gh extension install github/gh-copilot`

## Usage

### Show help

```bash
copilotx help
```

Displays a beautiful help screen with available commands.

### List available profiles

```bash
copilotx list
```

Shows all configured profiles in a formatted table. The last used profile is marked with `*`.

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

Interactive wizard with Spectre.Console prompts to add or update a profile. Features:
- Selection prompts for profile types
- Secure password input for API keys
- Validation and user-friendly error messages

## Configuration

Profiles are stored in `~/.copilotx/config.json`.

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

The returned token is used as `COPILOT_PROVIDER_API_KEY`.

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
# Add Azure OpenAI profile interactively
copilotx add

# List all profiles with beautiful table
copilotx list

# Use Azure profile
copilotx use azure-gpt suggest "create a function to sort an array"

# Switch back to default
copilotx default suggest "explain this code"

# Use last profile
copilotx last
```

## Development

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run -- list
dotnet run -- add
```

### Pack as Tool

```bash
dotnet pack
```

## Features

- Beautiful CLI interface with Spectre.Console
- Colored output and formatted tables
- Interactive prompts with selection menus
- Secure password input for API keys
- Error handling with user-friendly messages
- Persistent configuration storage

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

## License

MIT
