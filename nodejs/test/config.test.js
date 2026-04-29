const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const config = require('../config');
const foundry = require('../foundry');

function withIsolatedConfigDir(t) {
  const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'copilotx-node-test-'));
  const previousDir = process.env.COPILOTX_CONFIG_DIR;
  const previousScope = process.env.COPILOTX_CONFIG_SCOPE;

  process.env.COPILOTX_CONFIG_DIR = tempDir;
  process.env.COPILOTX_CONFIG_SCOPE = 'global';

  t.after(() => {
    if (previousDir === undefined) {
      delete process.env.COPILOTX_CONFIG_DIR;
    } else {
      process.env.COPILOTX_CONFIG_DIR = previousDir;
    }

    if (previousScope === undefined) {
      delete process.env.COPILOTX_CONFIG_SCOPE;
    } else {
      process.env.COPILOTX_CONFIG_SCOPE = previousScope;
    }

    fs.rmSync(tempDir, { recursive: true, force: true });
  });

  return tempDir;
}

function withoutConfigDirOverride(t) {
  const previousDir = process.env.COPILOTX_CONFIG_DIR;

  delete process.env.COPILOTX_CONFIG_DIR;

  t.after(() => {
    if (previousDir === undefined) {
      delete process.env.COPILOTX_CONFIG_DIR;
    } else {
      process.env.COPILOTX_CONFIG_DIR = previousDir;
    }
  });
}

test('sanitizeSegment normalizes unsupported characters', () => {
  const value = config.__test.sanitizeSegment('User Name+Team@contoso.com');
  assert.equal(value, 'user_name_team@contoso.com');
});

test('resolveConfigFileFor uses global config when scope is global', (t) => {
  withoutConfigDirOverride(t);

  const pathValue = config.__test.resolveConfigFileFor('global', 'tenant__user');
  assert.match(pathValue, /[\\/]\.copilotx[\\/]config\.json$/);
});

test('resolveConfigFileFor uses user-scoped config when identity is present', (t) => {
  withoutConfigDirOverride(t);

  const pathValue = config.__test.resolveConfigFileFor('auto', 'tenant__user');
  assert.match(pathValue, /[\\/]\.copilotx[\\/]config\.tenant__user\.json$/);
});

test('resolveConfigFileFor falls back to global when identity is missing', (t) => {
  withoutConfigDirOverride(t);

  const pathValue = config.__test.resolveConfigFileFor('azure-user', null);
  assert.match(pathValue, /[\\/]\.copilotx[\\/]config\.json$/);
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
    model: 'gpt-4o',
    providerType: 'azure',
    azureCliToken: 'auto',
    tokenScope: 'https://cognitiveservices.azure.com/.default'
  });
});
