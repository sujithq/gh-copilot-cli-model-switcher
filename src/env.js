const ENV_KEYS = [
  'COPILOT_PROVIDER_BASE_URL',
  'COPILOT_PROVIDER_API_KEY',
  'COPILOT_PROVIDER_TYPE',
  'COPILOT_MODEL',
];

export function buildEnvForProfile(profile, processEnv) {
  const nextEnv = { ...processEnv };

  for (const key of ENV_KEYS) delete nextEnv[key];

  if (!profile || profile.type === 'copilot') {
    return nextEnv;
  }

  if (profile.type !== 'byok' && profile.type !== 'proxy') {
    throw new Error(`Unknown profile type: ${profile.type}`);
  }

  if (!profile.baseUrl) {
    throw new Error('Profile is missing baseUrl');
  }

  nextEnv.COPILOT_PROVIDER_BASE_URL = profile.baseUrl;
  if (profile.providerType) nextEnv.COPILOT_PROVIDER_TYPE = profile.providerType;
  if (profile.model) nextEnv.COPILOT_MODEL = profile.model;

  if (profile.apiKey) {
    nextEnv.COPILOT_PROVIDER_API_KEY = profile.apiKey;
  } else if (profile.apiKeyEnv) {
    const val = processEnv[profile.apiKeyEnv];
    if (!val) {
      throw new Error(`Environment variable ${profile.apiKeyEnv} is not set`);
    }
    nextEnv.COPILOT_PROVIDER_API_KEY = val;
  }

  return nextEnv;
}

