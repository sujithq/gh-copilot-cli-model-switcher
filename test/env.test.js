import test from 'node:test';
import assert from 'node:assert/strict';

import { buildEnvForProfile } from '../src/env.js';

test('copilot profile clears BYOK env', () => {
  const env = buildEnvForProfile(
    { name: 'default', type: 'copilot' },
    {
      COPILOT_PROVIDER_BASE_URL: 'x',
      COPILOT_PROVIDER_API_KEY: 'y',
      COPILOT_MODEL: 'z',
      OTHER: 'keep',
    },
  );

  assert.equal(env.COPILOT_PROVIDER_BASE_URL, undefined);
  assert.equal(env.COPILOT_PROVIDER_API_KEY, undefined);
  assert.equal(env.COPILOT_MODEL, undefined);
  assert.equal(env.OTHER, 'keep');
});

test('byok profile sets expected env', () => {
  const env = buildEnvForProfile(
    {
      name: 'azure',
      type: 'byok',
      baseUrl: 'https://example',
      model: 'gpt',
      apiKeyEnv: 'AZ_KEY',
    },
    { AZ_KEY: 'secret', OTHER: 'keep' },
  );

  assert.equal(env.COPILOT_PROVIDER_BASE_URL, 'https://example');
  assert.equal(env.COPILOT_MODEL, 'gpt');
  assert.equal(env.COPILOT_PROVIDER_API_KEY, 'secret');
  assert.equal(env.OTHER, 'keep');
});

test('byok profile errors when apiKeyEnv missing', () => {
  assert.throws(
    () =>
      buildEnvForProfile(
        { type: 'byok', baseUrl: 'https://example', apiKeyEnv: 'MISSING' },
        {},
      ),
    /Environment variable MISSING is not set/,
  );
});

