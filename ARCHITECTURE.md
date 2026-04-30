# copilot-byok-model-switcher Architecture & Design

## 🎯 Design Goals

1. **Simplicity**: Easy to use, minimal configuration
2. **Flexibility**: Support multiple models and providers
3. **Persistence**: Remember last used configuration
4. **Enterprise-ready**: Support for Azure, RBAC, proxy scenarios
5. **Feature Parity**: Both Node.js and .NET implementations provide identical functionality

## 🏗️ System Architecture

### High-Level Overview

```
┌─────────────┐
│   User CLI  │
└──────┬──────┘
       │
       ▼
┌─────────────────┐
│  Command Router │
└──────┬──────────┘
       │
       ├─────────────┬─────────────┬──────────────┐
       ▼             ▼             ▼              ▼
  ┌────────┐   ┌─────────┐   ┌─────────┐   ┌──────────┐
  │  List  │   │   Use   │   │   Add   │   │   Last   │
  └────────┘   └─────────┘   └─────────┘   └──────────┘
                     │
                     ▼
            ┌────────────────┐
            │ Config Manager │
            └────────┬───────┘
                     │
         ┌───────────┴───────────┐
         ▼                       ▼
  ┌──────────────┐      ┌────────────────┐
  │ Load Profile │      │ Save Profile   │
  └──────┬───────┘      └────────────────┘
         │
         ▼
  ┌──────────────────┐
  │ Set Environment  │
  │   Variables      │
  └──────┬───────────┘
         │
         ▼
  ┌──────────────────┐
  │  Execute gh      │
  │    copilot       │
  └──────────────────┘
```

## 📦 Component Breakdown

### 1. Command Router

**Responsibility**: Parse CLI arguments and route to appropriate command handler

**Inputs**: Command-line arguments
**Outputs**: Execution result (exit code)

**Commands**:
- `list`: Display all profiles
- `use <profile>`: Switch to and execute with profile
- `add`: Interactive profile creation
- `last`: Use last used profile
- `default`: Use default Copilot profile
- `help`: Show help information

### 2. Config Manager

**Responsibility**: Manage profile storage and retrieval

**Storage Location**: `~/.copilot-byok-model-switcher/config.json`

**Methods**:
- `loadConfig()`: Read configuration from disk
- `saveConfig(config)`: Write configuration to disk
- `getProfile(name)`: Retrieve specific profile
- `addProfile(profile)`: Add or update profile
- `listProfiles()`: Get all profiles
- `setLastUsed(name)`: Update last used profile
- `getLastUsed()`: Get last used profile name

**Data Structure**:
```typescript
interface Config {
  profiles: Profile[]
  lastUsed: string
}

interface Profile {
  name: string
  type: 'copilot' | 'byok' | 'proxy'
  model?: string
  baseUrl?: string
    apiKeyEnv?: string
    apiKey?: string
    providerType?: string
    azureCliToken?: 'auto' | 'on' | 'off'
    tokenScope?: string
}
```

### 3. Profile Switcher

**Responsibility**: Set environment variables based on profile configuration

**Environment Variables**:
- `COPILOT_PROVIDER_BASE_URL`
- `COPILOT_PROVIDER_API_KEY`
- `COPILOT_MODEL`
- `COPILOT_PROVIDER_TYPE`

**Logic**:

```
IF profile.type == 'copilot':
    UNSET all environment variables
ELSE IF profile.type == 'byok' OR 'proxy':
    SET COPILOT_PROVIDER_BASE_URL = profile.baseUrl
    SET COPILOT_MODEL = profile.model

    RESOLVE apiKey from apiKeyEnv/apiKey
    DETERMINE azureCliToken mode (auto/on/off)

    IF azureCliToken enabled:
        RUN az account get-access-token
        SET COPILOT_PROVIDER_BEARER_TOKEN = <token>
        UNSET COPILOT_PROVIDER_API_KEY
    ELSE IF apiKey is present:
        SET COPILOT_PROVIDER_API_KEY = apiKey

    IF profile.providerType:
        SET COPILOT_PROVIDER_TYPE = profile.providerType

EXECUTE gh copilot

IF auth/token failure detected AND azureCliToken was used:
    REFRESH token via az account get-access-token
    RETRY gh copilot once
```

### 4. Copilot Launcher

**Responsibility**: Execute GitHub Copilot CLI with configured environment

**Process**:
1. Set environment variables
2. Spawn `gh copilot` process with arguments
3. Inherit stdio for interactive experience
4. Wait for process completion
5. Return exit code

## 🔄 Data Flow

### Adding a Profile

```
User Input
    ↓
Interactive Prompts
    ↓
Collect Profile Data
    ↓
Validate Input
    ↓
Load Existing Config
    ↓
Add/Update Profile
    ↓
Save Config to Disk
    ↓
Confirm Success
```

### Using a Profile

```
Command: copilot-byok-model-switcher use azure-gpt -p "help"
    ↓
Parse Arguments
    profile: "azure-gpt"
    copilot-args: ["-p", "help"]
    ↓
Load Config from Disk
    ↓
Find Profile "azure-gpt"
    ↓
Set Environment Variables
    COPILOT_PROVIDER_BASE_URL = "https://..."
    COPILOT_PROVIDER_API_KEY = ENV["AZURE_OPENAI_KEY"]
    COPILOT_MODEL = "gpt-4"
    ↓
Update lastUsed = "azure-gpt"
    ↓
Save Config to Disk
    ↓
Execute: gh copilot -p "help"
    ↓
Return Exit Code
```

## 🏢 Enterprise Architecture Patterns

### Pattern 1: Direct API Key

```
copilot-byok-model-switcher → GitHub Copilot CLI → Azure OpenAI (API Key)
```

**Use Case**: Environments where API keys are allowed
**Configuration**: Store API key in environment variable

### Pattern 2: RBAC with Proxy

```
copilot-byok-model-switcher → GitHub Copilot CLI → Proxy → Azure OpenAI (RBAC)
```

**Use Case**: Enterprise environments with RBAC-only access
**Proxy Responsibilities**:
- Token acquisition from Microsoft Entra ID
- Token refresh
- Request forwarding with proper authentication

**Configuration**: Point to proxy endpoint with proxy key

### Pattern 2b: RBAC Local Wrapper (No API Keys)

```
copilot-byok-model-switcher → Azure CLI token → GitHub Copilot CLI → Azure OpenAI (RBAC)
```

**Use Case**: API keys disabled, local developer workflow
**Configuration**:
- `azureCliToken: auto` or `on`
- `providerType: azure` (recommended)
- `tokenScope: https://cognitiveservices.azure.com/.default` (default)

### Pattern 3: Local Models

```
copilot-byok-model-switcher → GitHub Copilot CLI → Ollama/vLLM → Local Model
```

**Use Case**: Offline scenarios, privacy-sensitive environments
**Configuration**: Point to localhost endpoint

## 🔒 Security Considerations

### API Key Storage

**Problem**: Where to store sensitive API keys?

**Solutions**:
1. **Environment Variables** (Recommended):
   - Store in shell profile (`~/.bashrc`, `~/.zshrc`)
   - Use secrets management tools
   - Reference via `apiKeyEnv` in profile

2. **Direct Storage** (Not Recommended):
   - Store in config file with `apiKey` field
   - Less secure, easier to accidentally commit
   - Only for development/testing

### File Permissions

**Config File**: `~/.copilot-byok-model-switcher/config.json`
- Should have restricted permissions (600)
- Only user should have read/write access

### Token Rotation

For enterprise scenarios:
- Use proxy with automatic token refresh
- Don't store tokens directly in config
- Proxy handles short-lived token lifecycle

For local wrapper token mode:
- Token is acquired on each run via Azure CLI
- Token refresh + one-time retry is automatic on token/auth failures
- Requires local `az login` session

## 🔌 Implementation Differences

### Node.js Implementation

**Advantages**:
- Lightweight, fast startup
- Native npm ecosystem integration
- Easy to distribute via npm
- Cross-platform compatibility

**Libraries**:
- `yargs`: Argument parsing
- `readline`: Interactive prompts
- `child_process`: Process spawning

### .NET Implementation

**Advantages**:
- Beautiful CLI with Spectre.Console
- Strong typing with C#
- Better IDE support
- Native Windows integration

**Libraries**:
- `Spectre.Console`: Rich CLI formatting
- `System.Text.Json`: JSON serialization
- `System.Diagnostics`: Process management

**Key Differences**:
- Interactive prompts are more visually appealing
- Selection menus instead of text input
- Password masking for sensitive inputs
- Colored and formatted output

## 📊 Configuration Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "properties": {
    "profiles": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "name": { "type": "string" },
          "type": {
            "type": "string",
            "enum": ["copilot", "byok", "proxy"]
          },
          "model": { "type": "string" },
          "baseUrl": { "type": "string" },
          "apiKeyEnv": { "type": "string" },
          "apiKey": { "type": "string" },
          "providerType": { "type": "string" },
          "azureCliToken": { "type": "string", "enum": ["auto", "on", "off"] },
          "tokenScope": { "type": "string" }
        },
        "required": ["name", "type"]
      }
    },
    "lastUsed": { "type": "string" }
  },
  "required": ["profiles", "lastUsed"]
}
```

## 🚀 Extension Points

### Future Features

1. **Profile Templates**:
   ```
   copilot-byok-model-switcher add --template azure
   copilot-byok-model-switcher add --template ollama
   ```

2. **Per-Repository Profiles**:
   ```
   .copilot-byok-model-switcher.json in project root
   Override global config
   ```

3. **Shell Completion**:
   ```
   copilot-byok-model-switcher completion bash
   copilot-byok-model-switcher completion zsh
   ```

4. **Profile Import/Export**:
   ```
   copilot-byok-model-switcher export azure-profile > azure.json
   copilot-byok-model-switcher import azure.json
   ```

5. **Model Routing**:
   ```json
   {
     "routing": {
       "suggest": "gpt-4",
       "explain": "gpt-3.5-turbo"
     }
   }
   ```

## 📈 Performance Considerations

### Startup Time
- Config file is small, parsing is fast
- No external API calls during startup
- Direct process spawn, minimal overhead

### Config Size
- JSON format is human-readable but slightly verbose
- Typical config: < 5KB
- No performance impact

### Process Overhead
- Single process spawn to `gh copilot`
- Stdio inheritance for interactive experience
- No additional proxy overhead (unless configured)

## 🧪 Testing Strategy

### Unit Tests
- Config loading/saving
- Profile validation
- Environment variable setting

### Integration Tests
- Full command execution flow
- Profile switching
- Error handling

### Manual Testing
- Interactive prompts
- Different providers (Azure, OpenAI, Ollama)
- Error scenarios

## 📝 Error Handling

### Common Errors

1. **Profile Not Found**:
   ```
   Error: Profile 'xyz' not found
   Use 'copilot-byok-model-switcher list' to see available profiles.
   ```

2. **Missing API Key**:
   ```
   Warning: Environment variable AZURE_OPENAI_KEY is not set
   ```

3. **gh copilot Not Installed**:
   ```
   Error executing gh copilot: command not found
   Make sure GitHub Copilot CLI is installed:
   gh extension install github/gh-copilot
   ```

4. **Invalid Config**:
   ```
   Error loading config: JSON parse error
   Config file will be reset to defaults.
   ```

## 🔄 State Management

### Persistent State
- Profiles configuration
- Last used profile

### Transient State
- Current session environment variables
- Process state (gh copilot execution)

### State Transitions

```
Initial State (Default Profile)
    ↓
User: copilot-byok-model-switcher use azure-gpt
    ↓
Active State (Azure Profile)
    ↓
Save Last Used
    ↓
Execute gh copilot
    ↓
Return to Shell (State Persisted)
```

## 🎨 Design Principles

1. **Convention over Configuration**: Sensible defaults
2. **Don't Repeat Yourself**: Reuse profiles
3. **Fail Fast**: Clear error messages
4. **User-Friendly**: Interactive and intuitive
5. **Enterprise-Ready**: Support complex scenarios
6. **Secure by Default**: Environment variables for secrets

## 🔗 Integration Points

### GitHub Copilot CLI
- Environment variable interface
- Process spawning
- Stdio inheritance

### Shell Environment
- Environment variable reading
- Process execution
- Exit codes

### File System
- Config storage (~/.copilot-byok-model-switcher/)
- File permissions
- JSON serialization

## 📚 References

- [GitHub Copilot CLI BYOK Documentation](https://docs.github.com/en/copilot/customizing-copilot)
- [OpenAI API Specification](https://platform.openai.com/docs/api-reference)
- [Azure OpenAI Service](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [12-Factor App Principles](https://12factor.net/)
