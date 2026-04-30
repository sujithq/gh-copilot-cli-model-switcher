# copilot-byok-model-switcher Quick Reference

## Installation

### Node.js
```bash
cd nodejs && npm install && npm link
```

### .NET
```bash
cd dotnet/CopilotX && dotnet pack && dotnet tool install --global --add-source ./nupkg copilot-byok-model-switcher
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
| `copilot-byok-model-switcher list` | Show profiles with interactive selection menu | `copilot-byok-model-switcher list` → Enter profile # |
| `copilot-byok-model-switcher use <profile>` | Use specific profile | `copilot-byok-model-switcher use azure-gpt4` |
| `copilot-byok-model-switcher last` | Use last profile | `copilot-byok-model-switcher last` |
| `copilot-byok-model-switcher default` | Use default Copilot | `copilot-byok-model-switcher default` |
| `copilot-byok-model-switcher add` | Add new profile | `copilot-byok-model-switcher add` |
| `copilot-byok-model-switcher import-foundry` | Import from Foundry deployments | `copilot-byok-model-switcher import-foundry --mode each` |
| `copilot-byok-model-switcher help` | Show help | `copilot-byok-model-switcher help` |

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

### Azure OpenAI (API Keys Disabled)
```json
{
  "name": "azure-rbac-local",
  "type": "byok",
  "baseUrl": "https://xxx.openai.azure.com/openai/deployments/gpt-4",
  "model": "gpt-4",
  "providerType": "azure",
  "azureCliToken": "auto",
  "tokenScope": "https://cognitiveservices.azure.com/.default"
}
```

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

For keyless Azure profiles, copilot-byok-model-switcher uses Azure CLI:
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
copilot-byok-model-switcher use azure-gpt4
copilot-byok-model-switcher use openai
copilot-byok-model-switcher use ollama
```

### With Commands
```bash
copilot-byok-model-switcher use azure-gpt4 -p "create a function"
copilot-byok-model-switcher use ollama -p "what does this do"
copilot-byok-model-switcher last -p "another question"
```

### Quick Access
```bash
# Set preferred profile
copilot-byok-model-switcher use azure-gpt4

# Use it repeatedly
copilot-byok-model-switcher last -p "..."
copilot-byok-model-switcher last -p "..."
```

### Import Foundry Deployments
```bash
# Prompt for each discovered deployment
copilot-byok-model-switcher import-foundry --mode each

# Add all discovered deployments
copilot-byok-model-switcher import-foundry --all

# Import from one account/resource group
copilot-byok-model-switcher import-foundry --account myfoundry --resource-group my-rg --all
```

## Files

- **Config (active)**: shown by `copilot-byok-model-switcher list`
- **Global config**: `~/.copilot-byok-model-switcher/config.json`
- **Azure user-scoped config**: `~/.copilot-byok-model-switcher/config.<tenantId>__<userName>.json`
- **Scope override**: `COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE=auto|azure-user|global`
- **Node.js**: `/nodejs`
- **.NET**: `/dotnet/CopilotX`
- **Examples**: `/examples`

## Troubleshooting

### Profile not found
```bash
copilot-byok-model-switcher list  # Check available profiles
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
