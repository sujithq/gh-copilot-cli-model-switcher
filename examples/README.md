# copilot-byok-model-switcher Configuration Examples

This directory contains sample configuration files and usage examples for copilot-byok-model-switcher.

## Sample Configuration

See [`config.sample.json`](config.sample.json) for a complete example configuration with multiple profile types.

## Profile Examples

### Default GitHub Copilot

```json
{
  "name": "default",
  "type": "copilot",
  "model": "auto"
}
```

**Usage**:
```bash
gh-copilot-byok use default
```

### Azure OpenAI with API Key

```json
{
  "name": "azure-gpt4",
  "type": "byok",
  "baseUrl": "https://your-resource.openai.azure.com/openai/deployments/gpt-4",
  "apiKeyEnv": "AZURE_OPENAI_KEY",
  "model": "gpt-4"
}
```

**Setup**:
```bash
export AZURE_OPENAI_KEY="your-api-key-here"
```

**Usage**:
```bash
gh-copilot-byok use azure-gpt4 -p "create a function to parse JSON"
```

### Azure OpenAI with RBAC (API Keys Disabled, Local Wrapper)

```json
{
  "name": "azure-rbac-local",
  "type": "byok",
  "baseUrl": "https://your-resource.openai.azure.com/openai/deployments/gpt-4",
  "model": "gpt-4",
  "providerType": "azure",
  "azureCliToken": "auto",
  "tokenScope": "https://cognitiveservices.azure.com/.default"
}
```

**Setup**:
```bash
az login
```

**Usage**:
```bash
gh-copilot-byok use azure-rbac-local -p "create a function to parse JSON"
```

Notes:
- `azureCliToken: auto` detects Azure profiles and uses Azure CLI token when API key is not configured.
- On token/auth failures, gh-copilot-byok refreshes token and retries once.

### OpenAI API

```json
{
  "name": "openai-gpt4",
  "type": "byok",
  "baseUrl": "https://api.openai.com/v1",
  "apiKeyEnv": "OPENAI_API_KEY",
  "model": "gpt-4"
}
```

**Setup**:
```bash
export OPENAI_API_KEY="sk-..."
```

**Usage**:
```bash
gh-copilot-byok use openai-gpt4 -p "what does this code do"
```

### Ollama Local

```json
{
  "name": "ollama-llama3",
  "type": "byok",
  "baseUrl": "http://localhost:11434/v1",
  "model": "llama3"
}
```

**Prerequisites**:
```bash
# Install Ollama
curl -fsSL https://ollama.com/install.sh | sh

# Pull model
ollama pull llama3

# Start Ollama server (usually auto-starts)
ollama serve
```

**Usage**:
```bash
gh-copilot-byok use ollama-llama3 -p "write a hello world script"
```

### Azure OpenAI via APIM Proxy

For enterprise scenarios with RBAC and API Management:

```json
{
  "name": "azure-proxy",
  "type": "proxy",
  "baseUrl": "https://your-apim.azure-api.net",
  "apiKeyEnv": "APIM_SUBSCRIPTION_KEY",
  "model": "gpt-4",
  "providerType": "azure"
}
```

**Setup**:
```bash
export APIM_SUBSCRIPTION_KEY="your-subscription-key"
```

**Usage**:
```bash
gh-copilot-byok use azure-proxy -p "help me debug this"
```

## Usage Scenarios

### Scenario 1: Quick Development with OpenAI

```bash
# Set up API key once
export OPENAI_API_KEY="sk-..."

# Add profile
gh-copilot-byok add
# Enter: openai-dev, byok, https://api.openai.com/v1, gpt-4, env, OPENAI_API_KEY

# Use for coding
gh-copilot-byok use openai-dev -p "create a REST API endpoint"
gh-copilot-byok use openai-dev -p "how does this function work"
```

### Scenario 2: Enterprise Azure with Multiple Deployments

```bash
# Add production profile
gh-copilot-byok add
# Enter: azure-prod, byok, https://prod.openai.azure.com/..., gpt-4, env, AZURE_PROD_KEY

# Add development profile
gh-copilot-byok add
# Enter: azure-dev, byok, https://dev.openai.azure.com/..., gpt-4, env, AZURE_DEV_KEY

# Use different profiles for different tasks
gh-copilot-byok use azure-dev -p "test function"
gh-copilot-byok use azure-prod -p "production code"
```

### Scenario 3: Offline Development with Ollama

```bash
# Start Ollama
ollama pull llama3
ollama serve

# Add Ollama profile
gh-copilot-byok add
# Enter: local, byok, http://localhost:11434/v1, llama3, none

# Work offline
gh-copilot-byok use local -p "create a function"
gh-copilot-byok use local -p "what is this"
```

### Scenario 4: Switching Between Models

```bash
# List all profiles
gh-copilot-byok list

# Try different models for comparison
gh-copilot-byok use gpt-4 -p "optimize this code"
gh-copilot-byok use gpt-3.5 -p "optimize this code"
gh-copilot-byok use llama3 -p "optimize this code"

# Use last profile quickly
gh-copilot-byok last -p "another question"
```

### Scenario 5: Testing with Different Providers

```bash
# Test with OpenAI
gh-copilot-byok use openai-gpt4 -p "write unit tests for this function"

# Test with Azure
gh-copilot-byok use azure-gpt4 -p "write unit tests for this function"

# Test locally
gh-copilot-byok use ollama-llama3 -p "write unit tests for this function"

# Compare results and choose preferred provider
```

## Environment Variables Reference

### Azure OpenAI

```bash
export AZURE_OPENAI_KEY="your-key"
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com"
```

### OpenAI

```bash
export OPENAI_API_KEY="sk-..."
```

### API Management

```bash
export APIM_SUBSCRIPTION_KEY="your-subscription-key"
export APIM_BASE_URL="https://your-apim.azure-api.net"
```

### Multiple Environments

```bash
# Development
export AZURE_DEV_KEY="dev-key"

# Staging
export AZURE_STAGING_KEY="staging-key"

# Production
export AZURE_PROD_KEY="prod-key"
```

## Tips & Best Practices

### 1. Use Meaningful Profile Names

Good:
```
azure-gpt4-prod
azure-gpt35-dev
ollama-local-llama3
openai-gpt4
```

Bad:
```
profile1
test
my-profile
```

### 2. Organize by Purpose

```json
{
  "profiles": [
    // Default
    { "name": "default", "type": "copilot" },

    // Development
    { "name": "dev-gpt35", "type": "byok", ... },

    // Production
    { "name": "prod-gpt4", "type": "byok", ... },

    // Local/Offline
    { "name": "local-llama3", "type": "byok", ... }
  ]
}
```

### 3. Keep API Keys in Environment Variables

Do this:
```json
{
  "apiKeyEnv": "AZURE_OPENAI_KEY"
}
```

For keyless Azure RBAC profiles:
```json
{
  "providerType": "azure",
  "azureCliToken": "auto"
}
```

Not this:
```json
{
  "apiKey": "actual-key-here"  // Don't do this!
}
```

### 4. Use Last Profile for Quick Access

```bash
# Set up your preferred profile
gh-copilot-byok use azure-gpt4

# From now on, just use:
gh-copilot-byok last -p "..."
gh-copilot-byok last -p "..."
```

### 5. Test Locally First

```bash
# Test with Ollama before using paid APIs
gh-copilot-byok use ollama-local -p "is this approach correct?"

# Once confirmed, switch to production
gh-copilot-byok use azure-prod -p "implement the final version"
```

## Troubleshooting Examples

### Issue: API Key Not Found

```bash
# Check if environment variable is set
echo $AZURE_OPENAI_KEY

# If not, set it
export AZURE_OPENAI_KEY="your-key"

# Or add to shell profile
echo 'export AZURE_OPENAI_KEY="your-key"' >> ~/.bashrc
source ~/.bashrc
```

### Issue: Wrong Base URL

```bash
# List current configuration
gh-copilot-byok list

# Update profile
gh-copilot-byok add
# Use same name to update existing profile
```

### Issue: Model Not Found

```bash
# For Ollama, ensure model is pulled
ollama list
ollama pull llama3

# For Azure, check deployment name matches
```

## Advanced Configuration

### Multiple API Keys

```bash
# In ~/.bashrc or ~/.zshrc
export OPENAI_API_KEY_PERSONAL="sk-..."
export OPENAI_API_KEY_WORK="sk-..."

export AZURE_OPENAI_KEY_DEV="..."
export AZURE_OPENAI_KEY_PROD="..."
```

### Per-Project Configuration

You can copy the config file to project directories:

```bash
# Copy to project
cp ~/.copilot-byok-model-switcher/config.json ./my-project/.copilot-byok-model-switcher.json

# Use different profiles per project
# (Future enhancement)
```

### Shell Aliases

```bash
# Add to ~/.bashrc or ~/.zshrc
alias cx='gh-copilot-byok'
alias cxl='gh-copilot-byok last'
alias cxs='gh-copilot-byok use'

# Usage
cx list
cxl -p "create a function"
cxs azure-prod -p "this code"
```

## Sample Workflows

### Workflow 1: Daily Development

```bash
# Morning: Start with default Copilot
gh-copilot-byok default

# Need Azure features: Switch to Azure
gh-copilot-byok use azure-gpt4

# Working offline: Switch to Ollama
gh-copilot-byok use ollama-local

# Quick subsequent uses
gh-copilot-byok last
```

### Workflow 2: Code Review

```bash
# Review with multiple models
gh-copilot-byok use gpt-4 -p "review this code"
gh-copilot-byok use azure-gpt4 -p "review this code"
gh-copilot-byok use claude -p "review this code"

# Compare insights from different models
```

### Workflow 3: Testing Different Models

```bash
# Test prompt with each model
for profile in gpt-4 gpt-3.5 llama3; do
  echo "Testing with $profile"
  gh-copilot-byok use $profile -p "optimize this algorithm"
done
```

## Integration Examples

### With Git Hooks

```bash
# .git/hooks/pre-commit
#!/bin/bash
gh-copilot-byok use azure-dev -p "check code quality of staged files"
```

### With CI/CD

```bash
# GitHub Actions
- name: AI Code Review
  run: |
    export AZURE_OPENAI_KEY="${{ secrets.AZURE_OPENAI_KEY }}"
    gh-copilot-byok use azure-gpt4 -p "review changes"
```

### With VS Code Tasks

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "Copilot Prompt",
      "type": "shell",
      "command": "gh-copilot-byok last -p '${selectedText}'"
    }
  ]
}
```
