import { jest } from '@jest/globals';
import { tmpdir } from 'os';
import { join } from 'path';
import { rmSync, mkdirSync } from 'fs';

// Use a temp directory so tests don't pollute ~/.copilotx
const TEST_DIR = join(tmpdir(), `copilotx-test-${process.pid}`);

// Mock os.homedir to return our temp dir
jest.unstable_mockModule('os', () => ({
  homedir: () => TEST_DIR,
}));

// Re-import config AFTER mocking
const { loadConfig, saveConfig, getProfile, buildEnvForProfile, DEFAULT_CONFIG } =
  await import('../src/config.js');

beforeEach(() => {
  mkdirSync(TEST_DIR, { recursive: true });
});

afterEach(() => {
  rmSync(TEST_DIR, { recursive: true, force: true });
});

describe('loadConfig', () => {
  test('returns DEFAULT_CONFIG when file does not exist', () => {
    const config = loadConfig();
    expect(config.profiles).toHaveLength(1);
    expect(config.profiles[0].name).toBe('default');
    expect(config.profiles[0].type).toBe('copilot');
    expect(config.lastUsed).toBe('default');
  });

  test('returns parsed config when file exists', () => {
    const custom = {
      profiles: [{ name: 'my-profile', type: 'copilot', model: 'gpt-4o' }],
      lastUsed: 'my-profile',
    };
    saveConfig(custom);
    const loaded = loadConfig();
    expect(loaded).toEqual(custom);
  });
});

describe('saveConfig / loadConfig round-trip', () => {
  test('persists and reloads profiles', () => {
    const cfg = {
      profiles: [
        { name: 'default', type: 'copilot', model: 'auto' },
        {
          name: 'azure',
          type: 'byok',
          baseUrl: 'https://example.com',
          model: 'gpt-4o',
          apiKeyEnv: 'MY_KEY',
        },
      ],
      lastUsed: 'azure',
    };
    saveConfig(cfg);
    expect(loadConfig()).toEqual(cfg);
  });
});

describe('getProfile', () => {
  const config = {
    profiles: [
      { name: 'default', type: 'copilot', model: 'auto' },
      { name: 'azure', type: 'byok', baseUrl: 'https://x.com', model: 'gpt-4o' },
    ],
    lastUsed: 'default',
  };

  test('returns matching profile', () => {
    expect(getProfile(config, 'azure').name).toBe('azure');
  });

  test('returns undefined for unknown profile', () => {
    expect(getProfile(config, 'unknown')).toBeUndefined();
  });
});

describe('buildEnvForProfile', () => {
  test('copilot profile returns empty set and three unsets', () => {
    const { set, unset } = buildEnvForProfile({ name: 'default', type: 'copilot', model: 'auto' });
    expect(set).toEqual({});
    expect(unset).toContain('COPILOT_PROVIDER_BASE_URL');
    expect(unset).toContain('COPILOT_PROVIDER_API_KEY');
    expect(unset).toContain('COPILOT_MODEL');
  });

  test('byok profile with apiKeyEnv resolves key from process.env', () => {
    process.env.MY_API_KEY = 'secret-value';
    const { set } = buildEnvForProfile({
      name: 'azure',
      type: 'byok',
      baseUrl: 'https://example.com',
      model: 'gpt-4o',
      apiKeyEnv: 'MY_API_KEY',
    });
    expect(set['COPILOT_PROVIDER_BASE_URL']).toBe('https://example.com');
    expect(set['COPILOT_MODEL']).toBe('gpt-4o');
    expect(set['COPILOT_PROVIDER_API_KEY']).toBe('secret-value');
    delete process.env.MY_API_KEY;
  });

  test('byok profile with inline apiKey uses it directly', () => {
    const { set } = buildEnvForProfile({
      name: 'ollama',
      type: 'byok',
      baseUrl: 'http://localhost:11434',
      model: 'llama3',
      apiKey: 'inline-key',
    });
    expect(set['COPILOT_PROVIDER_API_KEY']).toBe('inline-key');
  });

  test('byok profile without key has no COPILOT_PROVIDER_API_KEY', () => {
    const { set } = buildEnvForProfile({
      name: 'ollama',
      type: 'byok',
      baseUrl: 'http://localhost:11434',
      model: 'llama3',
    });
    expect(set['COPILOT_PROVIDER_API_KEY']).toBeUndefined();
  });

  test('byok profile with providerType sets COPILOT_PROVIDER_TYPE', () => {
    const { set } = buildEnvForProfile({
      name: 'az',
      type: 'byok',
      baseUrl: 'https://x.com',
      model: 'gpt-4o',
      providerType: 'azure',
    });
    expect(set['COPILOT_PROVIDER_TYPE']).toBe('azure');
  });
});
