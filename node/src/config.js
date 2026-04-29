import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'fs';
import { homedir } from 'os';
import { join } from 'path';

export const CONFIG_DIR = join(homedir(), '.copilotx');
export const CONFIG_FILE = join(CONFIG_DIR, 'config.json');

export const DEFAULT_CONFIG = {
  profiles: [
    {
      name: 'default',
      type: 'copilot',
      model: 'auto',
    },
  ],
  lastUsed: 'default',
};

/**
 * Load the config from disk, returning defaults if the file does not exist.
 * @returns {object} parsed config
 */
export function loadConfig() {
  if (!existsSync(CONFIG_FILE)) {
    return structuredClone(DEFAULT_CONFIG);
  }
  try {
    const raw = readFileSync(CONFIG_FILE, 'utf8');
    return JSON.parse(raw);
  } catch {
    return structuredClone(DEFAULT_CONFIG);
  }
}

/**
 * Persist the config object to disk.
 * @param {object} config
 */
export function saveConfig(config) {
  if (!existsSync(CONFIG_DIR)) {
    mkdirSync(CONFIG_DIR, { recursive: true });
  }
  writeFileSync(CONFIG_FILE, JSON.stringify(config, null, 2) + '\n');
}

/**
 * Find a profile by name.
 * @param {object} config
 * @param {string} name
 * @returns {object|undefined}
 */
export function getProfile(config, name) {
  return config.profiles.find((p) => p.name === name);
}

/**
 * Build the environment variable map for a profile.
 * Returns an object with env vars to set and an array of names to unset.
 * @param {object} profile
 * @returns {{ set: Record<string,string>, unset: string[] }}
 */
export function buildEnvForProfile(profile) {
  if (profile.type === 'copilot') {
    return {
      set: {},
      unset: ['COPILOT_PROVIDER_BASE_URL', 'COPILOT_PROVIDER_API_KEY', 'COPILOT_MODEL'],
    };
  }

  const envVars = {
    COPILOT_PROVIDER_BASE_URL: profile.baseUrl || '',
    COPILOT_MODEL: profile.model || '',
  };

  if (profile.apiKeyEnv && process.env[profile.apiKeyEnv]) {
    envVars['COPILOT_PROVIDER_API_KEY'] = process.env[profile.apiKeyEnv];
  } else if (profile.apiKey) {
    envVars['COPILOT_PROVIDER_API_KEY'] = profile.apiKey;
  }

  if (profile.providerType) {
    envVars['COPILOT_PROVIDER_TYPE'] = profile.providerType;
  }

  return { set: envVars, unset: [] };
}
