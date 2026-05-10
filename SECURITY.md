# Security Policy

## Supported Versions

Only the latest release of `gh-copilot-byok` receives security fixes. Please ensure you are running the most recent version before reporting a vulnerability.

| Version | Supported          |
| ------- | ------------------ |
| Latest  | ✅ |
| Older   | ❌ |

## Reporting a Vulnerability

If you discover a security vulnerability in this project, **do not** open a public GitHub issue.

Instead, please report it privately using one of the following methods:

- **GitHub Private Vulnerability Reporting**: Use the [Security tab](https://github.com/sujithq/gh-copilot-cli-model-switcher/security/advisories/new) of this repository to submit a private advisory.
- **Email**: Contact the maintainer directly at the email address listed on the [GitHub profile](https://github.com/sujithq).

Please include as much detail as possible:

- A description of the vulnerability and its potential impact
- Steps to reproduce the issue
- The affected version(s) of `gh-copilot-byok`
- Any suggested mitigations or patches (optional)

You can expect an acknowledgement within **5 business days** and a resolution or status update within **14 business days**.

## Disclosure Policy

- Security issues are handled confidentially until a fix is released.
- Once a fix is available, a GitHub Security Advisory will be published and the CHANGELOG will be updated.
- Credit is given to reporters who wish to be acknowledged.

## Security Best Practices

When using `gh-copilot-byok`, follow these guidelines to keep your credentials safe:

### API Key Storage

- **Prefer `apiKeyEnv`** over `apiKey` in profile configurations. Store your actual API keys in environment variables and never hard-code them in the config file.

  ```json
  {
    "name": "azure-gpt",
    "type": "byok",
    "baseUrl": "https://your-resource.openai.azure.com/openai/deployments/your-deployment",
    "apiKeyEnv": "AZURE_OPENAI_KEY",
    "model": "gpt-4"
  }
  ```

- **Azure CLI Token Mode**: For Azure deployments where API keys are disabled, use `azureCliToken: "auto"` to authenticate via `az account get-access-token` instead of storing credentials in the config file.

### Config File Permissions

Profiles and (optionally) API keys are stored in `~/.copilot-byok-model-switcher/config.json`. Restrict access to this file:

```bash
chmod 600 ~/.copilot-byok-model-switcher/config.json
```

### Enterprise / RBAC Scenarios

- Use a proxy layer (e.g., Azure API Management or LiteLLM) with token-based authentication to avoid distributing API keys.
- Use Azure user-scoped config (`COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE=azure-user`) to keep profiles isolated per Azure identity when multiple users share a machine.

### Dependency Security

This tool depends on:

- **.NET 10 SDK** — keep your .NET runtime and SDK up to date.
- **Spectre.Console** — pinned to a specific version in `CopilotX.csproj`; update regularly.
- **GitHub Copilot CLI** (`gh extension install github/gh-copilot`) — update via `gh extension upgrade copilot`.

Automated dependency updates are managed via [Dependabot](.github/dependabot.yml).

### Tool Permissions

When running `gh-copilot-byok` in non-interactive (prompt) mode, `--allow-all-tools` is injected automatically. Use explicit `--allow-tool` or `--deny-tool` flags where possible to follow the principle of least privilege:

```bash
# Allow only the write tool
gh-copilot-byok use myprofile -p "fix the tests" --allow-tool=write

# Deny a high-risk tool
gh-copilot-byok use myprofile -p "explain this" --deny-tool=run_command
```
