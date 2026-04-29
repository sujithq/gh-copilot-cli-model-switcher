const fs = require('fs');
const path = require('path');
const os = require('os');
const { spawnSync } = require('child_process');

const CONFIG_DIR = path.join(os.homedir(), '.copilotx');

function sanitizeSegment(value) {
  return (value || 'unknown')
    .toLowerCase()
    .replace(/[^a-z0-9@._-]/g, '_');
}

function getAzureIdentityKey() {
  try {
    const result = spawnSync('az', ['account', 'show', '-o', 'json'], {
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'ignore']
    });

    if (result.status !== 0 || !result.stdout) {
      return null;
    }

    const account = JSON.parse(result.stdout);
    const tenantId = sanitizeSegment(account.tenantId);
    const userName = sanitizeSegment(account.user && account.user.name);

    if (!tenantId || !userName) {
      return null;
    }

    return `${tenantId}__${userName}`;
  } catch {
    return null;
  }
}

function resolveConfigFile() {
  const scope = (process.env.COPILOTX_CONFIG_SCOPE || 'auto').toLowerCase();
  return resolveConfigFileFor(scope, getAzureIdentityKey());
}

function resolveConfigFileFor(scope, identityKey) {
  const normalizedScope = (scope || 'auto').toLowerCase();

  if (normalizedScope === 'global') {
    return path.join(CONFIG_DIR, 'config.json');
  }

  if (normalizedScope === 'azure-user' || normalizedScope === 'auto') {
    if (identityKey) {
      return path.join(CONFIG_DIR, `config.${identityKey}.json`);
    }
  }

  return path.join(CONFIG_DIR, 'config.json');
}

const DEFAULT_CONFIG = {
  profiles: [
    {
      name: 'default',
      type: 'copilot',
      model: 'auto'
    }
  ],
  lastUsed: 'default'
};

function ensureConfigDir() {
  if (!fs.existsSync(CONFIG_DIR)) {
    fs.mkdirSync(CONFIG_DIR, { recursive: true });
  }
}

function loadConfig() {
  ensureConfigDir();

  const configFile = resolveConfigFile();

  if (!fs.existsSync(configFile)) {
    saveConfig(DEFAULT_CONFIG);
    return DEFAULT_CONFIG;
  }

  try {
    const data = fs.readFileSync(configFile, 'utf8');
    return JSON.parse(data);
  } catch (error) {
    console.error('Error loading config:', error.message);
    return DEFAULT_CONFIG;
  }
}

function saveConfig(config) {
  ensureConfigDir();

  const configFile = resolveConfigFile();

  try {
    fs.writeFileSync(configFile, JSON.stringify(config, null, 2), 'utf8');
    return true;
  } catch (error) {
    console.error('Error saving config:', error.message);
    return false;
  }
}

function getProfile(name) {
  const config = loadConfig();
  return config.profiles.find(p => p.name === name);
}

function addProfile(profile) {
  const config = loadConfig();

  const existingIndex = config.profiles.findIndex(p => p.name === profile.name);
  if (existingIndex >= 0) {
    config.profiles[existingIndex] = profile;
  } else {
    config.profiles.push(profile);
  }

  return saveConfig(config);
}

function listProfiles() {
  const config = loadConfig();
  return config.profiles;
}

function setLastUsed(name) {
  const config = loadConfig();
  config.lastUsed = name;
  return saveConfig(config);
}

function getLastUsed() {
  const config = loadConfig();
  return config.lastUsed;
}

module.exports = {
  loadConfig,
  saveConfig,
  getProfile,
  addProfile,
  listProfiles,
  setLastUsed,
  getLastUsed,
  CONFIG_FILE: resolveConfigFile,
  getConfigFile: resolveConfigFile,
  __test: {
    sanitizeSegment,
    resolveConfigFileFor
  }
};
