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

Read-only output. Use `copilotx manage` for interactive actions.

### Manage profiles (Use/Remove)

```bash
copilotx manage
```

Single interactive flow to:
- `Use` a selected profile
- `Remove` multiple profiles
- `Add` a new profile
- `Import` profiles from Foundry
- `Exit`

### Use a specific profile

```bash
copilotx use <profile> [copilot-args..]
```

Switch to a profile and run GitHub Copilot CLI with that configuration. Without extra arguments, launches `gh copilot` in interactive mode.

All arguments after the profile name are forwarded directly to `gh copilot`.

**Examples:**

```bash
# Interactive mode
copilotx use azure-gpt

# Sub-command passthrough
copilotx use azure-gpt suggest "how to list files"
copilotx use ollama-local explain "what is this code doing"

# Non-interactive prompt (-p / --prompt)
copilotx use azure-gpt -p "fix the failing tests"
copilotx use azure-gpt -p "refactor this function" --allow-tool=write

# Deny a specific tool
copilotx use azure-gpt -p "explain this code" --deny-tool=run_command

# Disable a named MCP server
copilotx use azure-gpt -p "fix the tests" --disable-mcp-server=foundry-mcp
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
copilotx last [copilot-args..]
```

Quick access to your most recently used profile. Accepts the same passthrough flags as `use`.

```bash
# Interactive mode
copilotx last

# Non-interactive prompt
copilotx last -p "explain this code"
copilotx last suggest "how to list files"
```

### Use default Copilot

```bash
copilotx default [copilot-args..]
```

Switch back to the default GitHub Copilot (no BYOK). Accepts the same passthrough flags as `use`.

```bash
# Interactive mode
copilotx default

# Non-interactive prompt
copilotx default -p "explain this code"
copilotx default suggest "how do I list files?"
```

### Add a new profile

```bash
copilotx add
```

Interactive wizard to add or update a profile.

If a profile with equivalent settings already exists (same provider/model/base URL/auth/token/MCP settings), CopilotX updates the existing profile instead of creating a duplicate.

### Remove profiles (multi-select)

```bash
copilotx remove [profile1 profile2 ...]
```

- Run without names for interactive multi-select removal.
- Pass names to remove multiple in one command.
- `default` is protected and is not removed.

```bash
# Interactive multi-select
copilotx remove

# Remove two by name
copilotx remove azure-gpt ollama-local
```

### Import profiles from Foundry deployments

```bash
copilotx import-foundry [options]
```

Discovers Azure OpenAI / Foundry accounts and deployments via Azure CLI and creates CopilotX profiles.

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

**Examples:**

```bash
# Discover all accounts and prompt per deployment (interactive)
copilotx import-foundry

# Explicit mode each
copilotx import-foundry --mode each

# Add all deployments without prompt
copilotx import-foundry --all

# Target one account/resource group
copilotx import-foundry --account myfoundry --resource-group my-rg --all

# Scope to a specific subscription
copilotx import-foundry --subscription 00000000-0000-0000-0000-000000000000 --all
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

For Azure BYOK profiles, CopilotX also enables an MCP compatibility mode by default to avoid provider tool-count limits (for example: `Invalid 'tools': array too long`).

**Selecting which MCP servers to disable:** The first time you launch an Azure BYOK profile interactively (no `-p`), you are prompted to choose which MCP servers to disable from the known-heavy list. Your selection is saved to the profile under `mcpCompatServers` and reused on every subsequent run — no re-prompting needed.

Default list of candidate servers: `foundry-mcp`, `context7`, `msx-mcp`, `azure`, `workiq`, `powerbi-remote`.

To change the selection later, edit `mcpCompatServers` in your config file, or remove the field so the prompt appears again next time. To disable the entire compat mode, set `COPILOTX_DISABLE_MCP_COMPAT=off`.

**Non-interactive mode & tool permissions:**

When you pass `-p`/`--prompt` (non-interactive mode), `gh copilot` cannot prompt for per-tool permission at runtime and will fail with `could not request permission from user`. CopilotX automatically adds `--allow-all-tools` for you so scripts and piped usage work without extra flags.

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
copilotx use myprofile -p "fix the tests"

# Explicit override: only allow the write tool
copilotx use myprofile -p "fix the tests" --allow-tool=write

# Restrict further with --deny-tool even when --allow-all-tools is injected
copilotx use myprofile -p "fix the tests" --deny-tool=run_command
```

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

# Use Azure profile interactively
copilotx use azure-gpt

# Use Azure profile with a prompt (non-interactive)
copilotx use azure-gpt -p "create a function to sort an array"

# Use Azure profile with suggest sub-command
copilotx use azure-gpt suggest "create a function to sort an array"

# Switch back to default
copilotx default suggest "explain this code"

# Use last profile
copilotx last

# Use last profile non-interactively
copilotx last -p "fix the failing test"

# Import all Foundry deployments
copilotx import-foundry --all
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
