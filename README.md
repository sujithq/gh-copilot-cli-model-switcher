# gh-copilot-cli-model-switcher

Lightweight wrappers around GitHub Copilot CLI that make it easy to switch between:

- Default Copilot models
- BYOK (Bring Your Own Model) / OpenAI-compatible endpoints
- Enterprise proxy setups (Azure OpenAI / Foundry via APIM / LiteLLM)

Profiles are stored locally in `~/.copilotx/config.json`.

## Node.js CLI (yargs)

### Install

```bash
npm install
npm link
```

### Usage

```bash
copilotx list
copilotx add azure-gpt --type byok --baseUrl https://example.openai.azure.com/openai/deployments/gpt --model gpt-4o --apiKeyEnv AZURE_OPENAI_KEY
copilotx use azure-gpt

# Default Copilot mode
copilotx default

# Forward extra args to `copilot`
copilotx use azure-gpt -- chat "hello"
```

## .NET Tool (Spectre.Console + System.CommandLine)

### Build

```bash
dotnet build ./gh-copilot-cli-model-switcher.sln
```

### Run

```bash
dotnet run --project ./src-dotnet/CopilotX -- list
dotnet run --project ./src-dotnet/CopilotX -- add azure-gpt --type byok --baseUrl https://example --model gpt-4o --apiKeyEnv AZURE_OPENAI_KEY
dotnet run --project ./src-dotnet/CopilotX -- use azure-gpt
```

## Config format

Example `~/.copilotx/config.json`:

```json
{
  "profiles": [
    { "name": "default", "type": "copilot", "model": "auto" },
    {
      "name": "azure-gpt",
      "type": "byok",
      "baseUrl": "https://xxx.openai.azure.com/openai/deployments/gpt",
      "apiKeyEnv": "AZURE_OPENAI_KEY",
      "model": "gpt"
    },
    { "name": "ollama-local", "type": "byok", "baseUrl": "http://localhost:11434", "model": "llama3" },
    { "name": "apim-proxy", "type": "proxy", "baseUrl": "http://localhost:4000", "apiKey": "internal-key" }
  ],
  "lastUsed": "azure-gpt"
}
```

## Notes on Azure / Foundry (RBAC)

Copilot CLI BYOK expects an API key-like value via `COPILOT_PROVIDER_API_KEY`. If your enterprise disables keys and only allows Entra ID (RBAC), use a proxy (APIM/LiteLLM/vLLM) that:

- acquires and refreshes Entra ID tokens
- exposes an OpenAI-compatible `/v1/chat/completions` endpoint

Then point `baseUrl` at that proxy.
