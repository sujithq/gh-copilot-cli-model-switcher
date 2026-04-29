const fs = require('fs');
const path = require('path');
const os = require('os');

const CONFIG_DIR = path.join(os.homedir(), '.copilotx');
const CONFIG_FILE = path.join(CONFIG_DIR, 'config.json');

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

  if (!fs.existsSync(CONFIG_FILE)) {
    saveConfig(DEFAULT_CONFIG);
    return DEFAULT_CONFIG;
  }

  try {
    const data = fs.readFileSync(CONFIG_FILE, 'utf8');
    return JSON.parse(data);
  } catch (error) {
    console.error('Error loading config:', error.message);
    return DEFAULT_CONFIG;
  }
}

function saveConfig(config) {
  ensureConfigDir();

  try {
    fs.writeFileSync(CONFIG_FILE, JSON.stringify(config, null, 2), 'utf8');
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
  CONFIG_FILE
};
