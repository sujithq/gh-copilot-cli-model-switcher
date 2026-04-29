# copilotx (.NET)

A **Spectre.Console**-based CLI tool for switching between GitHub Copilot CLI model profiles.

## Requirements

- .NET 9 SDK or later

## Installation

### As a global .NET tool (once published)

```bash
dotnet tool install -g CopilotX
```

### Build and run locally

```bash
cd dotnet/CopilotX
dotnet build -c Release
dotnet run -- <command>
```

### Install as a global tool from local source

```bash
cd dotnet/CopilotX
dotnet pack -c Release
dotnet tool install -g --add-source ./nupkg CopilotX
```

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

## Configuration

Profiles are stored at `~/.copilotx/config.json`.

### Example config

```json
{
  "profiles": [
    {
      "name": "default",
      "type": "Copilot",
      "model": "auto"
    },
    {
      "name": "azure-gpt",
      "type": "Byok",
      "baseUrl": "https://xxx.openai.azure.com/openai/deployments/gpt-4o",
      "apiKeyEnv": "AZURE_OPENAI_KEY",
      "model": "gpt-4o"
    },
    {
      "name": "ollama-local",
      "type": "Byok",
      "baseUrl": "http://localhost:11434",
      "model": "llama3"
    }
  ],
  "lastUsed": "azure-gpt"
}
```

## Profile types

### `Copilot` (built-in Copilot)

Uses the built-in GitHub Copilot model. Clears any BYOK environment variables.

### `Byok` (Bring Your Own Key)

Connects to an external OpenAI-compatible endpoint.

| Field | Description |
|-------|-------------|
| `baseUrl` | Provider base URL |
| `model` | Model name |
| `apiKeyEnv` | Name of env var containing the API key (preferred) |
| `apiKey` | Inline API key (plain text – use `apiKeyEnv` instead) |
| `providerType` | Optional provider hint (`azure`, `openai`, etc.) |

## Shell integration (persistent env switching)

`copilotx use <profile>` launches `copilot` as a child process with the env vars applied.  
To export variables into your current shell session instead:

```bash
# bash / zsh
eval "$(copilotx env azure-gpt --shell bash)"

# fish
copilotx env azure-gpt --shell fish | source

# PowerShell
copilotx env azure-gpt --shell powershell | Invoke-Expression
```

## Development

```bash
cd dotnet/CopilotX
dotnet build
dotnet test       # (tests project coming soon)
```
