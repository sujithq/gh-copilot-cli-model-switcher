# copilotx (Node.js)

A **yargs**-based CLI tool for switching between GitHub Copilot CLI model profiles.

## Requirements

- Node.js ≥ 18
- npm ≥ 9

## Installation

```bash
# From the node/ directory
npm install
npm link          # installs `copilotx` globally from the local source
```

Or install from npm (once published):

```bash
npm install -g copilotx
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

## Profile types

### `copilot` (built-in Copilot)

Uses the built-in GitHub Copilot model. Clears any BYOK environment variables.

```json
{ "name": "default", "type": "copilot", "model": "auto" }
```

### `byok` (Bring Your Own Key)

Connects to an external OpenAI-compatible endpoint.

| Field | Description |
|-------|-------------|
| `baseUrl` | Provider base URL |
| `model` | Model name |
| `apiKeyEnv` | Name of env var containing the API key (preferred) |
| `apiKey` | Inline API key (plain text – use `apiKeyEnv` instead) |
| `providerType` | Optional provider hint (`azure`, `openai`, etc.) |

## Shell integration (persistent env switching)

`copilotx use <profile>` launches `copilot` as a child process with the env vars
applied.  
If you want to **export** the variables into your current shell session instead:

```bash
# bash / zsh
eval "$(copilotx env azure-gpt --shell bash)"

# fish
copilotx env azure-gpt --shell fish | source

# PowerShell
copilotx env azure-gpt --shell powershell | Invoke-Expression
```

You can also add a shell function to your `.bashrc` / `.zshrc`:

```bash
function cx() {
  eval "$(copilotx env "$1" --shell bash)"
}
# Usage: cx azure-gpt
```

## Development

```bash
npm install
npm test
```
