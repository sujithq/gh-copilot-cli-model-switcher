const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const config = require('../config');
const foundry = require('../foundry');

function withIsolatedConfigDir(t) {
  const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'copilot-byok-model-switcher-node-test-'));
  const previousDir = process.env.COPILOT_BYOK_MODEL_SWITCHER_CONFIG_DIR;
  const previousScope = process.env.COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE;

  process.env.COPILOT_BYOK_MODEL_SWITCHER_CONFIG_DIR = tempDir;
  process.env.COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE = 'global';

  t.after(() => {
    if (previousDir === undefined) {
      delete process.env.COPILOT_BYOK_MODEL_SWITCHER_CONFIG_DIR;
    } else {
      process.env.COPILOT_BYOK_MODEL_SWITCHER_CONFIG_DIR = previousDir;
    }

    if (previousScope === undefined) {
      delete process.env.COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE;
    } else {
      process.env.COPILOT_BYOK_MODEL_SWITCHER_CONFIG_SCOPE = previousScope;
    }

    fs.rmSync(tempDir, { recursive: true, force: true });
  });

  return tempDir;
}

function withoutConfigDirOverride(t) {
  const previousDir = process.env.COPILOT_BYOK_MODEL_SWITCHER_CONFIG_DIR;

  delete process.env.COPILOT_BYOK_MODEL_SWITCHER_CONFIG_DIR;

  t.after(() => {
    if (previousDir === undefined) {
      delete process.env.COPILOT_BYOK_MODEL_SWITCHER_CONFIG_DIR;
    } else {
      process.env.COPILOT_BYOK_MODEL_SWITCHER_CONFIG_DIR = previousDir;
    }
  });
}

test('sanitizeSegment normalizes unsupported characters', () => {
  const value = config.__test.sanitizeSegment('User Name+Team@contoso.com');
  assert.equal(value, 'user_name_team@contoso.com');
});

test('resolveConfigFileFor uses global config when scope is global', (t) => {
  withoutConfigDirOverride(t);

  const pathValue = config.__test.resolveConfigFileFor('global', 'tenant__user', path.join(os.tmpdir(), '.copilot-byok-model-switcher'));
  assert.match(pathValue, /[\\/]\.copilot-byok-model-switcher[\\/]config\.json$/);
});

test('resolveConfigFileFor uses user-scoped config when identity is present', (t) => {
  withoutConfigDirOverride(t);

  const pathValue = config.__test.resolveConfigFileFor('auto', 'tenant__user', path.join(os.tmpdir(), '.copilot-byok-model-switcher'));
  assert.match(pathValue, /[\\/]\.copilot-byok-model-switcher[\\/]config\.tenant__user\.json$/);
});

test('resolveConfigFileFor falls back to global when identity is missing', (t) => {
  withoutConfigDirOverride(t);

  const pathValue = config.__test.resolveConfigFileFor('azure-user', null, path.join(os.tmpdir(), '.copilot-byok-model-switcher'));
  assert.match(pathValue, /[\\/]\.copilot-byok-model-switcher[\\/]config\.json$/);
});

test('loadConfig creates default config file in isolated directory', (t) => {
  const tempDir = withIsolatedConfigDir(t);

  const loaded = config.loadConfig();
  const configFile = path.join(tempDir, 'config.json');

  assert.equal(loaded.lastUsed, 'default');
  assert.equal(Array.isArray(loaded.profiles), true);
  assert.equal(fs.existsSync(configFile), true);
});

test('addProfile inserts and updates by name', (t) => {
  withIsolatedConfigDir(t);

  const first = {
    name: 'azure-gpt',
    type: 'byok',
    model: 'gpt-4',
    baseUrl: 'https://example.openai.azure.com/openai/deployments/gpt-4'
  };

  const updated = {
    name: 'azure-gpt',
    type: 'byok',
    model: 'gpt-4.1',
    baseUrl: 'https://example.openai.azure.com/openai/deployments/gpt-4-1'
  };

  assert.equal(config.addProfile(first), true);
  assert.equal(config.addProfile(updated), true);

  const profiles = config.listProfiles();
  const matches = profiles.filter((p) => p.name === 'azure-gpt');

  assert.equal(matches.length, 1);
  assert.equal(matches[0].model, 'gpt-4.1');
});

test('addProfile does not create duplicates with equivalent settings', (t) => {
  withIsolatedConfigDir(t);

  const first = {
    name: 'azure-primary',
    type: 'byok',
    model: 'gpt-4.1',
    baseUrl: 'https://example.openai.azure.com/openai/deployments/gpt-4-1',
    providerType: 'azure',
    azureCliToken: 'auto',
    tokenScope: 'https://cognitiveservices.azure.com/.default',
    mcpCompatServers: ['azure', 'foundry-mcp']
  };

  const sameSettingsDifferentName = {
    name: 'azure-duplicate-name',
    type: 'byok',
    model: 'gpt-4.1',
    baseUrl: 'https://example.openai.azure.com/openai/deployments/gpt-4-1',
    providerType: 'azure',
    azureCliToken: 'auto',
    tokenScope: 'https://cognitiveservices.azure.com/.default',
    mcpCompatServers: ['foundry-mcp', 'azure']
  };

  assert.equal(config.addProfile(first), true);
  assert.equal(config.addProfile(sameSettingsDifferentName), true);

  const profiles = config.listProfiles();
  const matches = profiles.filter((p) => p.name === 'azure-primary' || p.name === 'azure-duplicate-name');

  assert.equal(matches.length, 1);
  assert.equal(matches[0].name, 'azure-primary');
});

test('removeProfiles removes multiple and resets lastUsed when needed', (t) => {
  withIsolatedConfigDir(t);

  config.addProfile({ name: 'p1', type: 'byok', model: 'model-1' });
  config.addProfile({ name: 'p2', type: 'byok', model: 'model-2' });
  config.setLastUsed('p2');

  const result = config.removeProfiles(['p1', 'p2']);
  assert.equal(result.ok, true);
  assert.equal(result.removed, 2);
  assert.equal(config.getLastUsed(), 'default');
});

test('setLastUsed persists across reads', (t) => {
  withIsolatedConfigDir(t);

  config.setLastUsed('azure-gpt');

  assert.equal(config.getLastUsed(), 'azure-gpt');
});

test('getProfile returns undefined for missing profile', (t) => {
  withIsolatedConfigDir(t);

  assert.equal(config.getProfile('does-not-exist'), undefined);
});

test('mapDeployment falls back to deployment name when model metadata is missing', () => {
  const mapped = foundry.mapDeployment({ name: 'gpt-4o-prod' });

  assert.deepEqual(mapped, {
    deploymentName: 'gpt-4o-prod',
    modelName: 'gpt-4o-prod',
    modelVersion: ''
  });
});

test('isChatCapableDeployment accepts chat and rejects embeddings', () => {
  const chat = foundry.isChatCapableDeployment({
    properties: { capabilities: { chatCompletion: 'true' }, model: { name: 'gpt-4.1' } }
  });

  const embedding = foundry.isChatCapableDeployment({
    properties: { capabilities: { embeddings: 'true' }, model: { name: 'text-embedding-ada-002' } }
  });

  assert.equal(chat, true);
  assert.equal(embedding, false);
});

test('isApplicableAccount accepts AIServices with flattened endpoint', () => {
  const applicable = foundry.isApplicableAccount({
    name: 'myfoundry',
    kind: 'AIServices',
    endpoint: 'https://myfoundry.cognitiveservices.azure.com/'
  });

  assert.equal(applicable, true);
});

test('buildUniqueProfileName appends suffix when base name already exists', () => {
  const name = foundry.buildUniqueProfileName('My Foundry', 'GPT-4o', [
    'foundry-my-foundry-gpt-4o',
    'foundry-my-foundry-gpt-4o-2'
  ]);

  assert.equal(name, 'foundry-my-foundry-gpt-4o-3');
});

test('buildImportedProfile creates azure token-based profile for deployment', () => {
  const profile = foundry.buildImportedProfile(
    'myfoundry',
    'https://myfoundry.openai.azure.com/',
    {
      deploymentName: 'gpt-4o-prod',
      modelName: 'gpt-4o',
      modelVersion: '2024-11-20'
    },
    []
  );

  assert.deepEqual(profile, {
    name: 'foundry-myfoundry-gpt-4o-prod',
    type: 'byok',
    baseUrl: 'https://myfoundry.openai.azure.com/openai/deployments/gpt-4o-prod',
    model: 'gpt-4o-prod',
    providerType: 'azure',
    azureCliToken: 'auto',
    tokenScope: 'https://cognitiveservices.azure.com/.default'
  });
});
