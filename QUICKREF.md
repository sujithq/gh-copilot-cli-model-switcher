# CopilotX Quick Reference

## Installation

### Node.js
```bash
cd nodejs && npm install && npm link
```

### .NET
```bash
cd dotnet/CopilotX && dotnet pack && dotnet tool install --global --add-source ./nupkg CopilotX
```

## Commands

| Command | Description | Example |
|---------|-------------|---------|
| `copilotx list` | Show all profiles | `copilotx list` |
| `copilotx use <profile>` | Use specific profile | `copilotx use azure-gpt4` |
| `copilotx last` | Use last profile | `copilotx last` |
| `copilotx default` | Use default Copilot | `copilotx default` |
| `copilotx add` | Add new profile | `copilotx add` |
| `copilotx help` | Show help | `copilotx help` |

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

## Common Workflows

### Switch Models
```bash
copilotx use azure-gpt4
copilotx use openai
copilotx use ollama
```

### With Commands
```bash
copilotx use azure-gpt4 suggest "create a function"
copilotx use ollama explain "what does this do"
copilotx last suggest "another question"
```

### Quick Access
```bash
# Set preferred profile
copilotx use azure-gpt4

# Use it repeatedly
copilotx last suggest "..."
copilotx last explain "..."
```

## Files

- **Config**: `~/.copilotx/config.json`
- **Node.js**: `/nodejs`
- **.NET**: `/dotnet/CopilotX`
- **Examples**: `/examples`

## Troubleshooting

### Profile not found
```bash
copilotx list  # Check available profiles
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
