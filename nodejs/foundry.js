function sanitizeProfilePart(value) {
  return (value || '')
    .toLowerCase()
    .replace(/[^a-z0-9-]/g, '-')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '');
}

function isApplicableAccount(account) {
  const endpoint = ((account && account.endpoint) || (account && account.properties && account.properties.endpoint) || '').toLowerCase();
  const kind = (account && account.kind || '').toLowerCase();
  return endpoint.includes('.openai.azure.com')
    || endpoint.includes('.cognitiveservices.azure.com')
    || kind.includes('openai')
    || kind === 'aiservices';
}

function mapDeployment(item) {
  const deploymentName = item && item.name || '';
  const propertiesModel = item && item.properties && item.properties.model || {};
  const rootModel = item && item.model || {};

  return {
    deploymentName,
    modelName: propertiesModel.name || rootModel.name || deploymentName,
    modelVersion: propertiesModel.version || rootModel.version || ''
  };
}

function isChatCapableDeployment(item) {
  const capabilities = item && item.properties && item.properties.capabilities || {};
  const chatCompletion = String(capabilities.chatCompletion || '').toLowerCase() === 'true';
  const responses = String(capabilities.responses || '').toLowerCase() === 'true';

  if (chatCompletion || responses) {
    return true;
  }

  // Fallback when capabilities are absent: exclude known embedding models.
  const modelName = ((item && item.properties && item.properties.model && item.properties.model.name)
    || (item && item.model && item.model.name)
    || (item && item.name)
    || '').toLowerCase();

  return !modelName.includes('embed');
}

function buildUniqueProfileName(accountName, deploymentName, existingNames) {
  const baseName = `foundry-${sanitizeProfilePart(accountName)}-${sanitizeProfilePart(deploymentName)}`;
  const taken = new Set(existingNames || []);

  if (!taken.has(baseName)) {
    return baseName;
  }

  let counter = 2;
  while (taken.has(`${baseName}-${counter}`)) {
    counter += 1;
  }

  return `${baseName}-${counter}`;
}

function buildImportedProfile(accountName, endpoint, deployment, existingNames) {
  const normalizedEndpoint = (endpoint || `https://${accountName}.openai.azure.com`).replace(/\/$/, '');
  const profileName = buildUniqueProfileName(accountName, deployment.deploymentName, existingNames);

  return {
    name: profileName,
    type: 'byok',
    baseUrl: `${normalizedEndpoint}/openai/deployments/${deployment.deploymentName}`,
    // For Azure OpenAI BYOK, COPILOT_MODEL must match deployment name.
    model: deployment.deploymentName,
    providerType: 'azure',
    azureCliToken: 'auto',
    tokenScope: 'https://cognitiveservices.azure.com/.default'
  };
}

module.exports = {
  sanitizeProfilePart,
  isApplicableAccount,
  isChatCapableDeployment,
  mapDeployment,
  buildUniqueProfileName,
  buildImportedProfile
};
