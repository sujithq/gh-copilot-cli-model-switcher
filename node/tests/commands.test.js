import { jest } from '@jest/globals';
import { tmpdir } from 'os';
import { join } from 'path';
import { rmSync, mkdirSync, writeFileSync, existsSync } from 'fs';

const TEST_DIR = join(tmpdir(), `copilotx-cmd-test-${process.pid}`);

jest.unstable_mockModule('os', () => ({
  homedir: () => TEST_DIR,
}));

const { loadConfig, saveConfig } = await import('../src/config.js');

beforeEach(() => {
  mkdirSync(join(TEST_DIR, '.copilotx'), { recursive: true });
});

afterEach(() => {
  rmSync(TEST_DIR, { recursive: true, force: true });
});

// Helper to write a config directly
function writeTestConfig(config) {
  writeFileSync(join(TEST_DIR, '.copilotx', 'config.json'), JSON.stringify(config, null, 2));
}

describe('list command output', () => {
  test('loadConfig finds profiles after saveConfig', () => {
    const cfg = {
      profiles: [
        { name: 'default', type: 'copilot', model: 'auto' },
        { name: 'azure', type: 'byok', baseUrl: 'https://x.com', model: 'gpt-4o', apiKeyEnv: 'MY_KEY' },
      ],
      lastUsed: 'azure',
    };
    saveConfig(cfg);
    const loaded = loadConfig();
    expect(loaded.profiles).toHaveLength(2);
    expect(loaded.lastUsed).toBe('azure');
  });
});

describe('remove command logic', () => {
  test('removing a profile removes it and updates lastUsed', () => {
    const cfg = {
      profiles: [
        { name: 'default', type: 'copilot', model: 'auto' },
        { name: 'azure', type: 'byok', baseUrl: 'https://x.com', model: 'gpt-4o' },
      ],
      lastUsed: 'azure',
    };
    saveConfig(cfg);

    const loaded = loadConfig();
    loaded.profiles = loaded.profiles.filter((p) => p.name !== 'azure');
    if (loaded.lastUsed === 'azure') {
      loaded.lastUsed = loaded.profiles[0]?.name || null;
    }
    saveConfig(loaded);

    const after = loadConfig();
    expect(after.profiles).toHaveLength(1);
    expect(after.profiles[0].name).toBe('default');
    expect(after.lastUsed).toBe('default');
  });
});

describe('default command logic', () => {
  test('setting defaultProfile persists', () => {
    const cfg = {
      profiles: [
        { name: 'default', type: 'copilot', model: 'auto' },
        { name: 'azure', type: 'byok', baseUrl: 'https://x.com', model: 'gpt-4o' },
      ],
      lastUsed: 'default',
    };
    saveConfig(cfg);

    const loaded = loadConfig();
    loaded.defaultProfile = 'azure';
    saveConfig(loaded);

    const after = loadConfig();
    expect(after.defaultProfile).toBe('azure');
  });
});
