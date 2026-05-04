# copilot-byok-model-switcher - .NET Tool

A lightweight .NET CLI tool wrapper around GitHub Copilot CLI for easy model switching between default Copilot and custom BYOK models.

Built with [Spectre.Console](https://spectreconsole.net/) for a beautiful CLI experience.

## Installation

### Install as a .NET Global Tool

```bash
cd dotnet/CopilotX
dotnet pack
dotnet tool install --global --add-source ./nupkg gh-copilot-byok
```

### Install from GitHub Packages (NuGet feed)

```bash
dotnet tool install --global gh-copilot-byok --add-source "https://nuget.pkg.github.com/sujithq/index.json"
```

This will make the `gh-copilot-byok` command available globally.

### Uninstall

```bash
dotnet tool uninstall --global gh-copilot-byok
```

### Update

```bash
dotnet tool update --global gh-copilot-byok --add-source "https://nuget.pkg.github.com/sujithq/index.json"
```

## CI/CD and Publishing

This repository includes:

- `.github/workflows/ci.yml`
  - Node tests
  - .NET build/tests
  - pack validation
- `.github/workflows/release-dotnet-tool.yml`
  - publishes the .NET tool to GitHub Packages NuGet feed when a release is published
- `.github/workflows/release-please.yml`
  - computes semantic versions and generates release notes

Install from GitHub Packages requires authenticated NuGet source configuration.

Full release instructions: [../../RELEASING.md](../../RELEASING.md)

## Prerequisites

- .NET 10 SDK or higher
- GitHub Copilot CLI installed: `gh extension install github/gh-copilot`

## Usage

### Show help

```bash
gh-copilot-byok help
```

Displays a beautiful help screen with available commands.

### List available profiles

```bash
gh-copilot-byok list
```

Shows all configured profiles in a formatted table. The last used profile is marked with `*`.

Read-only output. Use `gh-copilot-byok manage` for interactive actions.

### Manage profiles (Use/Remove)

```bash
gh-copilot-byok manage
```

Single interactive flow to:
- `Use` a selected profile
- `Remove` multiple profiles
- `Add` a new profile
- `Import` profiles from Foundry
- `MCP` set/reset compatibility server selections for Azure BYOK/proxy profiles
- `Exit`

### Set or reset MCP compatibility servers

```bash
gh-copilot-byok mcp-compat <profile> [--action set|reset|all|none]
```

For Azure BYOK/proxy profiles, this controls which MCP servers gh-copilot-byok disables automatically in compatibility mode.

- `set`: interactive server selection
- `reset`: clear saved selection and prompt again on next interactive `use`
- `all`: select all discovered/default candidate servers
- `none`: disable none

```bash
# Interactively set MCP compatibility servers
gh-copilot-byok mcp-compat foundry-myaccount-gpt-4-1

# Reset to prompt again on next interactive use
gh-copilot-byok mcp-compat foundry-myaccount-gpt-4-1 --action reset

# Disable none
gh-copilot-byok mcp-compat foundry-myaccount-gpt-4-1 --action none
```

### Use a specific profile

```bash
gh-copilot-byok use <profile> [copilot-args..]
```

Switch to a profile and run GitHub Copilot CLI with that configuration. Without extra arguments, launches `gh copilot` in interactive mode.

All arguments after the profile name are forwarded directly to `gh copilot`.

**Examples:**

```bash
# Interactive mode
gh-copilot-byok use azure-gpt

# Prompt mode examples
gh-copilot-byok use azure-gpt -p "how to list files"
gh-copilot-byok use ollama-local -p "what is this code doing"

# Non-interactive prompt (-p / --prompt)
gh-copilot-byok use azure-gpt -p "fix the failing tests"
gh-copilot-byok use azure-gpt -p "refactor this function" --allow-tool=write

# Deny a specific tool
gh-copilot-byok use azure-gpt -p "explain this code" --deny-tool=run_command

# Disable a named MCP server
gh-copilot-byok use azure-gpt -p "fix the tests" --disable-mcp-server=foundry-mcp
```

**Common passthrough flags:**

| Flag | Description |
|---|---|
| `-p` / `--prompt <text>` | Run non-interactively with a prompt |
| `--allow-all-tools` | Allow all tools (auto-injected in `-p` mode) |
| `--allow-all` / `--yolo` | Allow all tools and operations |
| `--allow-tool <name>` | Allow only a specific named tool |
| `--deny-tool <name>` | Deny a specific named tool |
| `--disable-mcp-server <name>` | Disable a named MCP server |
| `--disable-builtin-mcps` | Disable all built-in MCP servers |

### Use the last used profile

```bash
gh-copilot-byok last [copilot-args..]
```

Quick access to your most recently used profile. Accepts the same passthrough flags as `use`.

```bash
# Interactive mode
gh-copilot-byok last

# Non-interactive prompt
gh-copilot-byok last -p "explain this code"
gh-copilot-byok last -p "how to list files"
```

### Use default Copilot

```bash
gh-copilot-byok default [copilot-args..]
```

Switch back to the default GitHub Copilot (no BYOK). Accepts the same passthrough flags as `use`.

```bash
# Interactive mode
gh-copilot-byok default

# Non-interactive prompt
gh-copilot-byok default -p "explain this code"
gh-copilot-byok default -p "how do I list files?"
```

### Add a new profile

```bash
gh-copilot-byok add
```

Interactive wizard with Spectre.Console prompts to add or update a profile. Features:
- Selection prompts for profile types
- Secure password input for API keys
- Validation and user-friendly error messages

If a profile with equivalent settings already exists (same provider/model/base URL/auth/token/MCP settings), gh-copilot-byok updates the existing profile instead of creating a duplicate.

### Remove profiles (multi-select)

```bash
gh-copilot-byok remove [profile1 profile2 ...]
```

- Run without names for interactive multi-select removal.
- Pass names to remove multiple in one command.
- `default` is protected and is not removed.

```bash
# Interactive multi-select
gh-copilot-byok remove

# Remove two by name
gh-copilot-byok remove azure-gpt ollama-local
```

### Import profiles from Foundry deployments

```bash
gh-copilot-byok import-foundry [options]
```

Discovers Azure OpenAI / Foundry accounts and deployments via Azure CLI and creates gh-copilot-byok profiles.

Only chat-capable deployments are imported (embeddings are skipped).
On re-import, equivalent profiles are deduplicated automatically.

**Options:**

| Option | Description |
|---|---|
| `--account <name>` | Limit import to a single named account |
| `--resource-group <rg>` | Resource group of the account (required with `--account`) |
| `--subscription <id\|name>` | Scope discovery to a specific subscription |
| `--mode each\|all` | Prompt per deployment (`each`) or add all without prompts (`all`) |
| `--all` | Shorthand for `--mode all` |
| `--max-output-tokens <n>` | Set `maxOutputTokens` on imported profiles |
| `--max-prompt-tokens <n>` | Set `maxPromptTokens` on imported profiles |

When `import-foundry` runs in fully interactive mode (no `--mode`/`--all` and no token-limit flags), it asks once for optional default token limits.

In `--mode each` (interactive per deployment), you can override those defaults per deployment before profile creation.

**Examples:**

```bash
# Discover all accounts and prompt per deployment (interactive)
gh-copilot-byok import-foundry

# Explicit mode each
gh-copilot-byok import-foundry --mode each

# Add all deployments without prompt
gh-copilot-byok import-foundry --all

# Add all deployments with explicit token limits
gh-copilot-byok import-foundry --all --max-output-tokens 4096 --max-prompt-tokens 64000

# Prompt per deployment and optionally override token limits per model
gh-copilot-byok import-foundry --mode each --max-output-tokens 4096 --max-prompt-tokens 64000

# Target one account/resource group
gh-copilot-byok import-foundry --account myfoundry --resource-group my-rg --all

# Scope to a specific subscription
gh-copilot-byok import-foundry --subscription 00000000-0000-0000-0000-000000000000 --all
```

## Configuration

Profiles are stored in an active config file under `~/.copilot-byok-model-switcher/`.

Config scope behavior:
- `COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE=auto` (default): use Azure user-scoped config when `az account show` is available, otherwise global config
- `COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE=azure-user`: always use Azure user-scoped config
- `COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE=global`: always use `~/.copilot-byok-model-switcher/config.json`

In Azure user-scoped mode, file name format is:
- `~/.copilot-byok-model-switcher/config.<tenantId>__<userName>.json`

Use `gh-copilot-byok list` to see which config file is currently active.

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
- `maxOutputTokens`: Optional max output tokens, mapped to `COPILOT_PROVIDER_MAX_OUTPUT_TOKENS`
- `maxPromptTokens`: Optional max prompt tokens, mapped to `COPILOT_PROVIDER_MAX_PROMPT_TOKENS`
- `maxTokens`: Legacy alias for `maxOutputTokens`; read for compatibility but not written to new configs

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
export COPILOT_PROVIDER_MAX_OUTPUT_TOKENS=<max output tokens>
export COPILOT_PROVIDER_MAX_PROMPT_TOKENS=<max prompt tokens>
gh copilot
```

`maxOutputTokens` is useful when you want to cap generated output as Copilot CLI usage shifts from request-based to token-based accounting. If you also need to constrain how much context is sent to the provider, set `maxPromptTokens`.

If `azureCliToken` is enabled (or `auto` detects Azure profile with no API key), gh-copilot-byok runs:

```bash
az account get-access-token --scope https://cognitiveservices.azure.com/.default --query accessToken -o tsv
```

The returned token is set as `COPILOT_PROVIDER_BEARER_TOKEN`. `COPILOT_PROVIDER_API_KEY` is cleared in token mode to avoid auth-mode ambiguity.

For Azure BYOK profiles, gh-copilot-byok also enables an MCP compatibility mode by default to avoid provider tool-count limits (for example: `Invalid 'tools': array too long`).

**Selecting which MCP servers to disable:** The first time you launch an Azure BYOK profile interactively (no `-p`), you are prompted to choose which MCP servers to disable from the known-heavy list. Your selection is saved to the profile under `mcpCompatServers` and reused on every subsequent run — no re-prompting needed.

Default list of candidate servers: `foundry-mcp`, `context7`, `msx-mcp`, `azure`, `workiq`, `powerbi-remote`.

To change the selection later, edit `mcpCompatServers` in your config file, or remove the field so the prompt appears again next time. To disable the entire compat mode, set `CBMS_DISABLE_MCP_COMPAT=off`.

**Non-interactive mode & tool permissions:**

When you pass `-p`/`--prompt` (non-interactive mode), `gh copilot` cannot prompt for per-tool permission at runtime and will fail with `could not request permission from user`. gh-copilot-byok automatically adds `--allow-all-tools` for you so scripts and piped usage work without extra flags.

If you already specify any of the permission flags below, auto-injection is skipped:

| Flag | Effect |
|---|---|
| `--allow-all-tools` | Allow all tools (default auto-injection) |
| `--allow-all` | Allow all tools and operations |
| `--yolo` | Alias for `--allow-all` |
| `--allow-tool <name>` | Allow a specific tool only |

Examples:

```bash
# Auto-injection: --allow-all-tools is added for you
gh-copilot-byok use myprofile -p "fix the tests"

# Explicit override: only allow the write tool
gh-copilot-byok use myprofile -p "fix the tests" --allow-tool=write

# Restrict further with --deny-tool even when --allow-all-tools is injected
gh-copilot-byok use myprofile -p "fix the tests" --deny-tool=run_command
```

Retry behavior:
- If `gh copilot` fails with token/auth-related errors, gh-copilot-byok refreshes the token and retries once.

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

`import-foundry` uses Azure CLI to discover deployments and create/update profiles in `~/.copilot-byok-model-switcher/config.json`.

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
# Add Azure OpenAI profile interactively
gh-copilot-byok add

# List all profiles with beautiful table
gh-copilot-byok list

# Use Azure profile interactively
gh-copilot-byok use azure-gpt

# Use Azure profile with a prompt (non-interactive)
gh-copilot-byok use azure-gpt -p "create a function to sort an array"

# Use Azure profile with prompt mode
gh-copilot-byok use azure-gpt -p "create a function to sort an array"

# Switch back to default
gh-copilot-byok default -p "explain this code"

# Use last profile
gh-copilot-byok last

# Use last profile non-interactively
gh-copilot-byok last -p "fix the failing test"

# Import all Foundry deployments
gh-copilot-byok import-foundry --all
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

### Test

```bash
dotnet run --project ../CopilotX.Tests/CopilotX.Tests.csproj
```

Unit tests use xUnit and execute via `dotnet run`.
Current tests include config scope/path resolution, default config creation, profile upsert semantics, and last-used persistence using isolated temp config directories.

## Features

- Beautiful CLI interface with Spectre.Console
- Colored output and formatted tables
- Interactive prompts with selection menus
- Secure password input for API keys
- Error handling with user-friendly messages
- Persistent configuration storage

## Troubleshooting

### "Profile not found"
Run `gh-copilot-byok list` to see available profiles or `gh-copilot-byok add` to create a new one.

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
