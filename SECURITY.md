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

- **Microsoft Entra authentication (.NET 2.4.0+)**: Prefer `authentication: { "type": "entra", "tenant": "<tenant-id>" }` for keyless deployments. Set the appropriate resource audience for the endpoint. Entra profiles reject stored credentials, validate tenant and token expiry, and pass credentials only in the Copilot child environment. Legacy `azureCliToken` profiles remain supported.

### Entra Authentication Boundaries

- Use only trusted HTTPS endpoints: the configured endpoint receives the bearer token. Tenant validation pins the tenant, not a particular user or managed identity.
- No access token is written to switcher profiles or diagnostics. Azure CLI still maintains its own authentication cache; protect it and use `az logout` where appropriate.
- Local privileged processes and child processes can access process environments. Child-only injection is not a credential vault or sandbox.
- Tokens are acquired at launch and can be cached by Azure CLI. There is no transparent refresh in a running Copilot process; restart through the launcher after expiry.
- Explicit Entra launches are not automatically replayed after authentication failure, avoiding duplicate tool actions.
- Native provider registries can override environment configuration. Explicit Entra launches fail closed on a conflicting registry rather than modifying it.
- Inference preflight is optional, can be billed, and does not assign roles. A `403` can also reflect network restrictions or policy, not just missing RBAC.

### Config File Permissions

Profiles and (optionally) API keys are stored in `~/.copilot-byok-model-switcher/config.json`. Restrict access to this file:

```bash
chmod 600 ~/.copilot-byok-model-switcher/config.json
```

### Enterprise / RBAC Scenarios

- Use explicit Entra profiles for launch-time authentication, or a trusted proxy layer when continuous token renewal is required.
- Use Azure user-scoped config (`COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE=azure-user`) to keep profiles isolated per Azure identity when multiple users share a machine.

### Dependency Security

This tool depends on:

- **.NET 10 SDK** — keep your .NET runtime and SDK up to date.
- **Spectre.Console** — pinned to a specific version in `CopilotX.csproj`; update regularly.
- **GitHub Copilot CLI** — explicit Entra profiles require the current standalone `copilot` executable; legacy profiles retain `gh copilot`. Follow [GitHub's installation guidance](https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli).

Automated dependency updates are managed via [Dependabot](.github/dependabot.yml).

### Tool Permissions

When running `gh-copilot-byok` in non-interactive (prompt) mode, `--allow-all-tools` is injected automatically. Use explicit `--allow-tool` or `--deny-tool` flags where possible to follow the principle of least privilege:

```bash
# Allow only the write tool
gh-copilot-byok use myprofile -p "fix the tests" --allow-tool=write

# Deny a high-risk tool
gh-copilot-byok use myprofile -p "explain this" --deny-tool=run_command
```
