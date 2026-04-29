const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const config = require('../config');

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

test('sanitizeSegment normalizes unsupported characters', () => {
  const value = config.__test.sanitizeSegment('User Name+Team@contoso.com');
  assert.equal(value, 'user_name_team@contoso.com');
});

test('resolveConfigFileFor uses global config when scope is global', () => {
  const pathValue = config.__test.resolveConfigFileFor('global', 'tenant__user');
  assert.match(pathValue, /[\\/]\.copilotx[\\/]config\.json$/);
});

test('resolveConfigFileFor uses user-scoped config when identity is present', () => {
  const pathValue = config.__test.resolveConfigFileFor('auto', 'tenant__user');
  assert.match(pathValue, /[\\/]\.copilotx[\\/]config\.tenant__user\.json$/);
});

test('resolveConfigFileFor falls back to global when identity is missing', () => {
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
