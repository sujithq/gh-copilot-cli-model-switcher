const test = require('node:test');
const assert = require('node:assert/strict');

const config = require('../config');

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
