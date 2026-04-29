import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';

const CONFIG_DIR = path.join(os.homedir(), '.copilotx');
const CONFIG_PATH = path.join(CONFIG_DIR, 'config.json');

function defaultConfig() {
  return {
    profiles: [{ name: 'default', type: 'copilot', model: 'auto' }],
    lastUsed: 'default',
  };
}

export function getConfigPath() {
  return CONFIG_PATH;
}

export async function loadConfig() {
  try {
    const raw = await fs.readFile(CONFIG_PATH, 'utf8');
    const parsed = JSON.parse(raw);

    if (!parsed || typeof parsed !== 'object') return defaultConfig();
    if (!Array.isArray(parsed.profiles)) parsed.profiles = [];
    if (!parsed.lastUsed) parsed.lastUsed = 'default';

    if (!parsed.profiles.some((p) => p?.name === 'default')) {
      parsed.profiles.unshift({ name: 'default', type: 'copilot', model: 'auto' });
    }

    return parsed;
  } catch (err) {
    if (err?.code === 'ENOENT') return defaultConfig();
    throw err;
  }
}

export async function saveConfig(config) {
  await fs.mkdir(CONFIG_DIR, { recursive: true });
  await fs.writeFile(CONFIG_PATH, JSON.stringify(config, null, 2) + '\n', 'utf8');
}

export function findProfile(config, name) {
  return config.profiles.find((p) => p?.name === name);
}

