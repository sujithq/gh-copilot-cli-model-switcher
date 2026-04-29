# gh-copilot-cli-model-switcher

**copilotx** – a lightweight CLI wrapper around [GitHub Copilot CLI](https://docs.github.com/en/copilot/using-github-copilot/using-github-copilot-in-the-command-line) that lets you quickly switch between:

- The **built-in Copilot** model (`copilot` profile type)
- **Custom (BYOK) models** – any OpenAI-compatible endpoint (`byok` profile type)

Profiles are stored locally in `~/.copilotx/config.json`.  
Two implementations are provided: a **Node.js / yargs** tool and a **.NET / Spectre.Console** tool.

---

## Quick start

### Node.js

```bash
cd node
npm install
npm link          # makes `copilotx` available globally

copilotx list
copilotx add
copilotx use azure-gpt
```

### .NET

```bash
cd dotnet/CopilotX
dotnet run -- list
dotnet run -- add
dotnet run -- use azure-gpt
```

---

## Commands

| Command | Description |
|---------|-------------|
| `copilotx list` | List all configured profiles |
| `copilotx use <profile>` | Switch to a profile and launch GitHub Copilot CLI |
| `copilotx add [name]` | Interactively add a new profile |
| `copilotx last` | Show the last-used profile |
| `copilotx last --use` | Re-activate and launch with the last-used profile |
| `copilotx default [profile]` | Show or set the default profile |
| `copilotx env <profile>` | Print shell export commands (eval-friendly) |
| `copilotx remove <profile>` | Delete a profile |

---

## Configuration file

`~/.copilotx/config.json`

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
      "baseUrl": "https://xxx.openai.azure.com/openai/deployments/gpt-4o",
      "apiKeyEnv": "AZURE_OPENAI_KEY",
      "model": "gpt-4o"
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

---

## Environment variables set by the tool

| Variable | Description |
|----------|-------------|
| `COPILOT_PROVIDER_BASE_URL` | Base URL of the OpenAI-compatible endpoint |
| `COPILOT_PROVIDER_API_KEY` | API key for the endpoint |
| `COPILOT_MODEL` | Model identifier |
| `COPILOT_PROVIDER_TYPE` | Optional provider type hint |

For `copilot`-type profiles, these variables are **unset** before launching Copilot CLI.

---

## Shell integration

To export variables into the **current shell** rather than spawning a child process:

```bash
# bash / zsh
eval "$(copilotx env azure-gpt --shell bash)"

# fish
copilotx env azure-gpt --shell fish | source

# PowerShell
copilotx env azure-gpt --shell powershell | Invoke-Expression
```

---

## Enterprise / Azure OpenAI

For RBAC-only Azure environments (no API key), use a proxy layer:

```
copilotx
  ↓
Proxy (APIM / LiteLLM) ← handles Entra ID token acquisition
  ↓
Azure OpenAI / Foundry (RBAC)
```

Configure the proxy endpoint as a `byok` profile:

```json
{
  "name": "azure-via-proxy",
  "type": "byok",
  "baseUrl": "http://localhost:4000",
  "apiKey": "internal-proxy-key",
  "model": "gpt-4o",
  "providerType": "azure"
}
```

---

## Repository structure

```
/
├── node/                  # Node.js yargs-based CLI
│   ├── src/
│   │   ├── index.js       # Entry point
│   │   ├── config.js      # Config helpers
│   │   └── commands/      # list, use, add, last, default, env, remove
│   ├── tests/             # Jest unit tests
│   ├── package.json
│   └── README.md
├── dotnet/
│   ├── CopilotX/          # .NET Spectre.Console CLI
│   │   ├── CopilotX.csproj
│   │   ├── Program.cs
│   │   ├── Models/        # Config, Profile
│   │   ├── Services/      # ConfigService
│   │   └── Commands/      # list, use, add, last, default, env, remove
│   └── README.md
└── README.md              # This file
```

---

## License

MIT
