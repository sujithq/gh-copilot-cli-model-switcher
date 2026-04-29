const fs = require('fs');
const path = require('path');
const os = require('os');
const { spawnSync } = require('child_process');

const DEFAULT_CONFIG_DIR = path.join(os.homedir(), '.copilotx');

function getConfigDir() {
  return process.env.COPILOTX_CONFIG_DIR || DEFAULT_CONFIG_DIR;
}

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

function resolveConfigFileFor(scope, identityKey, configDir = getConfigDir()) {
  const normalizedScope = (scope || 'auto').toLowerCase();

  if (normalizedScope === 'global') {
    return path.join(configDir, 'config.json');
  }

  if (normalizedScope === 'azure-user' || normalizedScope === 'auto') {
    if (identityKey) {
      return path.join(configDir, `config.${identityKey}.json`);
    }
  }

  return path.join(configDir, 'config.json');
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
  const configDir = getConfigDir();
  if (!fs.existsSync(configDir)) {
    fs.mkdirSync(configDir, { recursive: true });
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

function normalizeText(value) {
  return (value || '').trim().toLowerCase();
}

function normalizedMcpServers(profile) {
  const list = Array.isArray(profile && profile.mcpCompatServers)
    ? profile.mcpCompatServers
    : [];

  return [...new Set(list.map((s) => normalizeText(s)).filter(Boolean))].sort();
}

function buildProfileSettingsKey(profile) {
  const keyObject = {
    type: normalizeText(profile.type),
    model: normalizeText(profile.model),
    baseUrl: normalizeText(profile.baseUrl),
    apiKeyEnv: normalizeText(profile.apiKeyEnv),
    apiKey: normalizeText(profile.apiKey),
    providerType: normalizeText(profile.providerType),
    azureCliToken: normalizeText(profile.azureCliToken),
    tokenScope: normalizeText(profile.tokenScope),
    mcpCompatServers: normalizedMcpServers(profile)
  };

  return JSON.stringify(keyObject);
}

function upsertProfile(profile) {
  const config = loadConfig();
  const incomingName = (profile.name || '').trim();

  const byNameIndex = config.profiles.findIndex((p) => p.name === incomingName);
  if (byNameIndex >= 0) {
    config.profiles[byNameIndex] = profile;
    return {
      ok: saveConfig(config),
      action: 'updated-by-name',
      name: incomingName
    };
  }

  const incomingKey = buildProfileSettingsKey(profile);
  const equivalentIndex = config.profiles.findIndex((p) => buildProfileSettingsKey(p) === incomingKey);

  if (equivalentIndex >= 0) {
    const existingName = config.profiles[equivalentIndex].name;
    config.profiles[equivalentIndex] = { ...profile, name: existingName };
    return {
      ok: saveConfig(config),
      action: 'updated-equivalent',
      name: existingName
    };
  }

  config.profiles.push(profile);
  return {
    ok: saveConfig(config),
    action: 'added',
    name: incomingName
  };
}

function addProfile(profile) {
  return upsertProfile(profile).ok;
}

function removeProfiles(names) {
  const config = loadConfig();
  const targets = new Set((names || []).map((n) => (n || '').trim().toLowerCase()).filter(Boolean));

  if (!targets.size) {
    return { ok: true, removed: 0 };
  }

  const before = config.profiles.length;
  config.profiles = config.profiles.filter((p) => {
    if ((p.name || '').toLowerCase() === 'default') {
      return true;
    }

    return !targets.has((p.name || '').toLowerCase());
  });

  const removed = before - config.profiles.length;

  if (removed > 0 && targets.has((config.lastUsed || '').toLowerCase())) {
    config.lastUsed = config.profiles.some((p) => p.name === 'default')
      ? 'default'
      : (config.profiles[0] && config.profiles[0].name) || 'default';
  }

  return { ok: saveConfig(config), removed };
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
  upsertProfile,
  removeProfiles,
  CONFIG_FILE: resolveConfigFile,
  getConfigFile: resolveConfigFile,
  __test: {
    sanitizeSegment,
    resolveConfigFileFor,
    getConfigDir,
    buildProfileSettingsKey
  }
};
