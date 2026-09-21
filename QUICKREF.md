# copilot-byok-model-switcher Quick Reference

## Installation

### Node.js
```bash
cd nodejs && npm install && npm link
```

### .NET
```bash
cd dotnet/CopilotX && dotnet pack && dotnet tool install --global --add-source ./nupkg gh-copilot-byok
```

## Testing

### All tests
```powershell
./run-tests.ps1
```

### Node.js
```bash
cd nodejs && npm test
```

### .NET (xUnit)
```bash
cd dotnet/CopilotX.Tests && dotnet run
```

## Commands

| Command | Description | Example |
|---------|-------------|---------|
| `gh-copilot-byok list` | Show profiles with interactive selection menu | `gh-copilot-byok list` → Enter profile # |
| `gh-copilot-byok use <profile>` | Use specific profile | `gh-copilot-byok use azure-gpt4` |
| `gh-copilot-byok last` | Use last profile | `gh-copilot-byok last` |
| `gh-copilot-byok default` | Use default Copilot | `gh-copilot-byok default` |
| `gh-copilot-byok add` | Add new profile | `gh-copilot-byok add` |
| `gh-copilot-byok import-foundry` | Import from Foundry deployments | `gh-copilot-byok import-foundry --mode each` |
| `gh-copilot-byok help` | Show help | `gh-copilot-byok help` |

## Profile Types

### Default Copilot
```json
{
  "name": "default",
  "type": "copilot",
  "model": "auto"
}
```

### Azure OpenAI
```json
{
  "name": "azure",
  "type": "byok",
  "baseUrl": "https://xxx.openai.azure.com/openai/deployments/gpt-4",
  "apiKeyEnv": "AZURE_OPENAI_KEY",
  "model": "gpt-4"
}
```

### Azure OpenAI (API Keys Disabled, .NET 2.4.0+)
```json
{
  "name": "azure-rbac-local",
  "type": "byok",
  "baseUrl": "https://xxx.openai.azure.com/openai/v1",
  "model": "gpt-4.1",
  "deployment": "my-deployment",
  "providerType": "azure",
  "authentication": {
    "type": "entra",
    "tenant": "00000000-0000-0000-0000-000000000000",
    "resource": "https://ai.azure.com",
    "preflight": false
  }
}
```

Replace the tenant ID and deployment. Requires Azure CLI login and standalone `copilot`; launch with `gh-copilot-byok use azure-rbac-local`. Tokens are acquired per launch, not refreshed in a running session. The resource defaults to `https://ai.azure.com` for Foundry/OpenAI-v1 examples; use the Cognitive Services audience when your API surface requires it, not based on hostname alone. Preflight is opt-in and potentially billable. Native provider registries must not override the launch environment. See [enterprise guidance](README.md#enterprise-authentication-net-240).

Legacy `azureCliToken` / `tokenScope` profiles remain supported. Node.js is unchanged and does not support the new `authentication` schema.

### OpenAI
```json
{
  "name": "openai",
  "type": "byok",
  "baseUrl": "https://api.openai.com/v1",
  "apiKeyEnv": "OPENAI_API_KEY",
  "model": "gpt-4"
}
```

### Ollama
```json
{
  "name": "ollama",
  "type": "byok",
  "baseUrl": "http://localhost:11434/v1",
  "model": "llama3"
}
```

### Proxy (APIM)
```json
{
  "name": "proxy",
  "type": "proxy",
  "baseUrl": "https://your-apim.azure-api.net",
  "apiKeyEnv": "APIM_KEY",
  "model": "gpt-4"
}
```

## Environment Variables

```bash
# Azure OpenAI
export AZURE_OPENAI_KEY="your-key"

# OpenAI
export OPENAI_API_KEY="sk-..."

# API Management
export APIM_KEY="your-key"
```

For keyless Azure profiles, gh-copilot-byok uses Azure CLI:
```bash
az login
az account get-access-token --scope https://cognitiveservices.azure.com/.default
```

Auth toggle fields:
- `azureCliToken`: `auto` (default), `on`, `off`
- `tokenScope`: Azure scope for token requests

## Common Workflows

### Switch Models
```bash
gh-copilot-byok use azure-gpt4
gh-copilot-byok use openai
gh-copilot-byok use ollama
```

### With Commands
```bash
gh-copilot-byok use azure-gpt4 -p "create a function"
gh-copilot-byok use ollama -p "what does this do"
gh-copilot-byok last -p "another question"
```

### Quick Access
```bash
# Set preferred profile
gh-copilot-byok use azure-gpt4

# Use it repeatedly
gh-copilot-byok last -p "..."
gh-copilot-byok last -p "..."
```

### Import Foundry Deployments
```bash
# Prompt for each discovered deployment
gh-copilot-byok import-foundry --mode each

# Add all discovered deployments
gh-copilot-byok import-foundry --all

# Import from one account/resource group
gh-copilot-byok import-foundry --account myfoundry --resource-group my-rg --all
```

## Files

- **Config (active)**: shown by `gh-copilot-byok list`
- **Global config**: `~/.copilot-byok-model-switcher/config.json`
- **Azure user-scoped config**: `~/.copilot-byok-model-switcher/config.<tenantId>__<userName>.json`
- **Scope override**: `COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE=auto|azure-user|global`
- **Node.js**: `/nodejs`
- **.NET**: `/dotnet/CopilotX`
- **Examples**: `/examples`

## Troubleshooting

### Profile not found
```bash
gh-copilot-byok list  # Check available profiles
```

### API key missing
```bash
export AZURE_OPENAI_KEY="your-key"
```

### gh copilot not installed
```bash
gh extension install github/gh-copilot
```

## Documentation

- [README.md](README.md) - Main documentation
- [ARCHITECTURE.md](ARCHITECTURE.md) - Technical design
- [IMPLEMENTATION.md](IMPLEMENTATION.md) - Build summary
- [nodejs/README.md](nodejs/README.md) - Node.js guide
- [dotnet/CopilotX/README.md](dotnet/CopilotX/README.md) - .NET guide
- [examples/README.md](examples/README.md) - Usage examples

## Support

For issues and questions:
- Check documentation
- Review examples
- See architecture docs
