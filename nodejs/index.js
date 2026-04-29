#!/usr/bin/env node

const yargs = require('yargs/yargs');
const { hideBin } = require('yargs/helpers');
const { spawn } = require('child_process');
const readline = require('readline');
const {
  getProfile,
  addProfile,
  listProfiles,
  setLastUsed,
  getLastUsed,
  getConfigFile
} = require('./config');

function isAzureProfile(profile) {
  const baseUrl = (profile.baseUrl || '').toLowerCase();
  const providerType = (profile.providerType || '').toLowerCase();
  return baseUrl.includes('.openai.azure.com') || providerType === 'azure';
}

function shouldUseAzureCliToken(profile, hasApiKey) {
  const mode = (profile.azureCliToken || 'auto').toLowerCase();

  if (mode === 'on') {
    return true;
  }

  if (mode === 'off') {
    return false;
  }

  return !hasApiKey && isAzureProfile(profile);
}

function getAzureCliToken(profile) {
  const scope = profile.tokenScope || 'https://cognitiveservices.azure.com/.default';

  return new Promise((resolve, reject) => {
    const az = spawn(
      'az',
      ['account', 'get-access-token', '--scope', scope, '--query', 'accessToken', '-o', 'tsv'],
      { stdio: ['ignore', 'pipe', 'pipe'] }
    );

    let stdout = '';
    let stderr = '';

    az.stdout.on('data', (chunk) => {
      stdout += chunk.toString();
    });

    az.stderr.on('data', (chunk) => {
      stderr += chunk.toString();
    });

    az.on('error', (error) => {
      reject(new Error(`Failed to run az CLI: ${error.message}`));
    });

    az.on('close', (code) => {
      if (code !== 0) {
        reject(new Error(`az account get-access-token failed (exit ${code}): ${stderr.trim()}`));
        return;
      }

      const token = stdout.trim();
      if (!token) {
        reject(new Error('az CLI returned an empty access token'));
        return;
      }

      resolve(token);
    });
  });
}

async function setEnvironmentForProfile(profile) {
  if (profile.type === 'copilot') {
    delete process.env.COPILOT_PROVIDER_BASE_URL;
    delete process.env.COPILOT_PROVIDER_API_KEY;
    delete process.env.COPILOT_MODEL;
    delete process.env.COPILOT_PROVIDER_TYPE;
    return { usedAzureCliToken: false };
  } else if (profile.type === 'byok' || profile.type === 'proxy') {
    if (profile.baseUrl) {
      process.env.COPILOT_PROVIDER_BASE_URL = profile.baseUrl;
    }

    if (profile.model) {
      process.env.COPILOT_MODEL = profile.model;
    }

    let resolvedApiKey = '';

    if (profile.apiKeyEnv) {
      const apiKey = process.env[profile.apiKeyEnv];
      if (apiKey) {
        resolvedApiKey = apiKey;
      } else {
        console.warn(`Warning: Environment variable ${profile.apiKeyEnv} is not set`);
      }
    } else if (profile.apiKey) {
      resolvedApiKey = profile.apiKey;
    }

    const useAzureCliToken = shouldUseAzureCliToken(profile, !!resolvedApiKey);
    if (useAzureCliToken) {
      const token = await getAzureCliToken(profile);
      process.env.COPILOT_PROVIDER_API_KEY = token;
    } else if (resolvedApiKey) {
      process.env.COPILOT_PROVIDER_API_KEY = resolvedApiKey;
    } else {
      delete process.env.COPILOT_PROVIDER_API_KEY;
    }

    if (profile.providerType) {
      process.env.COPILOT_PROVIDER_TYPE = profile.providerType;
    }

    return { usedAzureCliToken: useAzureCliToken };
  }

  return { usedAzureCliToken: false };
}

function isTokenFailure(text) {
  return /(401|unauthorized|forbidden|invalid token|token expired|expired token|authentication failed|permission denied)/i.test(text || '');
}

function sanitizeProfilePart(value) {
  return (value || '')
    .toLowerCase()
    .replace(/[^a-z0-9-]/g, '-')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '');
}

function askQuestion(rl, prompt) {
  return new Promise((resolve) => rl.question(prompt, resolve));
}

function runAzJson(args) {
  return new Promise((resolve, reject) => {
    const az = spawn('az', args, { stdio: ['ignore', 'pipe', 'pipe'] });

    let stdout = '';
    let stderr = '';

    az.stdout.on('data', (chunk) => {
      stdout += chunk.toString();
    });

    az.stderr.on('data', (chunk) => {
      stderr += chunk.toString();
    });

    az.on('error', (error) => {
      reject(new Error(`Failed to run az CLI: ${error.message}`));
    });

    az.on('close', (code) => {
      if (code !== 0) {
        reject(new Error(`az ${args.join(' ')} failed (exit ${code}): ${stderr.trim()}`));
        return;
      }

      try {
        resolve(JSON.parse(stdout || '[]'));
      } catch (error) {
        reject(new Error(`Failed to parse az output: ${error.message}`));
      }
    });
  });
}

async function listFoundryAccounts(subscription) {
  const args = ['cognitiveservices', 'account', 'list', '-o', 'json'];
  if (subscription) {
    args.push('--subscription', subscription);
  }

  const accounts = await runAzJson(args);
  return accounts.filter((account) => {
    const endpoint = (account.properties?.endpoint || '').toLowerCase();
    const kind = (account.kind || '').toLowerCase();
    return endpoint.includes('.openai.azure.com') || kind.includes('openai');
  });
}

async function listAccountDeployments(accountName, resourceGroup, subscription) {
  const args = [
    'cognitiveservices',
    'account',
    'deployment',
    'list',
    '--name',
    accountName,
    '--resource-group',
    resourceGroup,
    '-o',
    'json'
  ];

  if (subscription) {
    args.push('--subscription', subscription);
  }

  const deployments = await runAzJson(args);
  return deployments.map((item) => ({
    deploymentName: item.name,
    modelName: item.properties?.model?.name || item.model?.name || item.name,
    modelVersion: item.properties?.model?.version || item.model?.version || '',
  }));
}

async function importFoundryProfiles(options) {
  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout
  });

  try {
    const accountArg = options.account || options.name || '';
    const rgArg = options.resourceGroup || options['resource-group'] || '';
    const subscription = options.subscription || '';
    const forcedMode = options.all ? 'all' : (options.mode || '');

    let mode = forcedMode;
    if (!mode) {
      mode = (await askQuestion(rl, 'Add mode (each/all) [each]: ') || 'each').trim().toLowerCase();
    }
    if (mode !== 'all') {
      mode = 'each';
    }

    let accounts;
    if (accountArg && rgArg) {
      accounts = [{ name: accountArg, resourceGroup: rgArg, properties: {} }];
    } else {
      accounts = await listFoundryAccounts(subscription);
    }

    if (!accounts.length) {
      console.log('No OpenAI/Foundry accounts found.');
      return 0;
    }

    let importedCount = 0;
    let scannedDeployments = 0;

    for (const account of accounts) {
      const accountName = account.name;
      const resourceGroup = account.resourceGroup;
      const endpoint = (account.properties?.endpoint || `https://${accountName}.openai.azure.com`).replace(/\/$/, '');

      if (!resourceGroup) {
        console.warn(`Skipping account ${accountName}: resource group not found.`);
        continue;
      }

      let deployments;
      try {
        deployments = await listAccountDeployments(accountName, resourceGroup, subscription);
      } catch (error) {
        console.warn(`Skipping ${accountName}: ${error.message}`);
        continue;
      }

      if (!deployments.length) {
        continue;
      }

      console.log(`\nAccount: ${accountName} (${resourceGroup})`);

      for (const dep of deployments) {
        scannedDeployments += 1;

        const modelLabel = dep.modelVersion ? `${dep.modelName}:${dep.modelVersion}` : dep.modelName;
        let shouldAdd = mode === 'all';

        if (!shouldAdd) {
          const answer = await askQuestion(
            rl,
            `Add deployment ${dep.deploymentName} (${modelLabel}) as profile? (y/N): `
          );
          shouldAdd = (answer || '').trim().toLowerCase() === 'y';
        }

        if (!shouldAdd) {
          continue;
        }

        const profileName = `foundry-${sanitizeProfilePart(accountName)}-${sanitizeProfilePart(dep.deploymentName)}`;

        const profile = {
          name: profileName,
          type: 'byok',
          baseUrl: `${endpoint}/openai/deployments/${dep.deploymentName}`,
          model: dep.modelName,
          providerType: 'azure',
          azureCliToken: 'auto',
          tokenScope: 'https://cognitiveservices.azure.com/.default'
        };

        if (addProfile(profile)) {
          importedCount += 1;
          console.log(`  Added profile: ${profileName}`);
        } else {
          console.warn(`  Failed to add profile: ${profileName}`);
        }
      }
    }

    console.log(`\nImported ${importedCount} profile(s) from ${scannedDeployments} deployment(s).`);
    return 0;
  } catch (error) {
    console.error(`Error importing Foundry profiles: ${error.message}`);
    console.error('Ensure Azure CLI is installed and authenticated: az login');
    return 1;
  } finally {
    rl.close();
  }
}

function runCopilot(copilotArgs) {
  return new Promise((resolve, reject) => {
    const copilot = spawn('gh', ['copilot', ...copilotArgs], {
      stdio: ['pipe', 'pipe', 'pipe'],
      env: process.env
    });

    let combined = '';

    process.stdin.pipe(copilot.stdin);

    copilot.stdout.on('data', (chunk) => {
      const text = chunk.toString();
      combined += text;
      process.stdout.write(text);
    });

    copilot.stderr.on('data', (chunk) => {
      const text = chunk.toString();
      combined += text;
      process.stderr.write(text);
    });

    copilot.on('error', (error) => {
      reject(error);
    });

    copilot.on('close', (code) => {
      process.stdin.unpipe(copilot.stdin);
      resolve({ code: code || 0, output: combined });
    });
  });
}

async function executeWithProfile(profileName, copilotArgs = []) {
  const profile = getProfile(profileName);

  if (!profile) {
    console.error(`Profile "${profileName}" not found.`);
    console.error('Use "copilotx list" to see available profiles.');
    return 1;
  }

  console.log(`Using profile: ${profile.name} (${profile.type})`);

  let envInfo;
  try {
    envInfo = await setEnvironmentForProfile(profile);
  } catch (error) {
    console.error(`Error setting auth environment: ${error.message}`);
    console.error('For Azure CLI token auth, ensure az is installed and you are logged in: az login');
    return 1;
  }

  setLastUsed(profileName);

  try {
    let result = await runCopilot(copilotArgs);

    if (result.code !== 0 && envInfo.usedAzureCliToken && isTokenFailure(result.output)) {
      console.warn('Detected token-related auth failure. Refreshing Azure CLI token and retrying once...');
      const refreshedToken = await getAzureCliToken(profile);
      process.env.COPILOT_PROVIDER_API_KEY = refreshedToken;
      result = await runCopilot(copilotArgs);
    }

    return result.code;
  } catch (error) {
    console.error('Error executing gh copilot:', error.message);
    console.error('Make sure GitHub Copilot CLI is installed: gh extension install github/gh-copilot');
    return 1;
  }
}

const argv = yargs(hideBin(process.argv))
  .scriptName('copilotx')
  .usage('$0 <command> [options]')
  .command(
    'list',
    'List all available profiles',
    () => {},
    () => {
      const profiles = listProfiles();
      const lastUsed = getLastUsed();

      console.log('Available profiles:');
      console.log('');

      profiles.forEach(profile => {
        const marker = profile.name === lastUsed ? '* ' : '  ';
        console.log(`${marker}${profile.name} (${profile.type})`);

        if (profile.type === 'byok' || profile.type === 'proxy') {
          console.log(`    Base URL: ${profile.baseUrl || 'N/A'}`);
          console.log(`    Model: ${profile.model || 'N/A'}`);
          if (profile.apiKeyEnv) {
            console.log(`    API Key: from ${profile.apiKeyEnv}`);
          }
        }

        console.log('');
      });

      console.log(`* = last used`);
      console.log(`\nConfig file: ${getConfigFile()}`);
    }
  )
  .command(
    'use <profile> [copilot-args..]',
    'Switch to a specific profile and run gh copilot',
    (yargs) => {
      yargs
        .positional('profile', {
          describe: 'Profile name to use',
          type: 'string'
        })
        .positional('copilot-args', {
          describe: 'Arguments to pass to gh copilot',
          type: 'array',
          default: []
        });
    },
    async (argv) => {
      const code = await executeWithProfile(argv.profile, argv['copilot-args'] || []);
      process.exit(code);
    }
  )
  .command(
    'last [copilot-args..]',
    'Use the last used profile and run gh copilot',
    (yargs) => {
      yargs.positional('copilot-args', {
        describe: 'Arguments to pass to gh copilot',
        type: 'array',
        default: []
      });
    },
    async (argv) => {
      const lastUsed = getLastUsed();
      if (!lastUsed) {
        console.error('No profile has been used yet.');
        process.exit(1);
      }
      const code = await executeWithProfile(lastUsed, argv['copilot-args'] || []);
      process.exit(code);
    }
  )
  .command(
    'add',
    'Add or update a profile interactively',
    () => {},
    async () => {
      const rl = readline.createInterface({
        input: process.stdin,
        output: process.stdout
      });

      const question = (prompt) => {
        return new Promise((resolve) => {
          rl.question(prompt, resolve);
        });
      };

      try {
        const name = await question('Profile name: ');
        if (!name.trim()) {
          console.error('Profile name cannot be empty');
          rl.close();
          process.exit(1);
        }

        const type = await question('Profile type (copilot/byok/proxy) [copilot]: ') || 'copilot';

        const profile = { name: name.trim(), type: type.trim() };

        if (type === 'byok' || type === 'proxy') {
          const baseUrl = await question('Base URL: ');
          if (baseUrl.trim()) {
            profile.baseUrl = baseUrl.trim();
          }

          const model = await question('Model: ');
          if (model.trim()) {
            profile.model = model.trim();
          }

          const apiKeyChoice = await question('API Key source (env/direct/none) [env]: ') || 'env';

          if (apiKeyChoice === 'env') {
            const apiKeyEnv = await question('Environment variable name: ');
            if (apiKeyEnv.trim()) {
              profile.apiKeyEnv = apiKeyEnv.trim();
            }
          } else if (apiKeyChoice === 'direct') {
            const apiKey = await question('API Key: ');
            if (apiKey.trim()) {
              profile.apiKey = apiKey.trim();
            }
          }

          const providerType = await question('Provider type (optional): ');
          if (providerType.trim()) {
            profile.providerType = providerType.trim();
          }

          const azureCliToken = await question('Azure CLI token mode (auto/on/off) [auto]: ') || 'auto';
          if (azureCliToken.trim()) {
            profile.azureCliToken = azureCliToken.trim().toLowerCase();
          }

          if (profile.azureCliToken === 'auto' || profile.azureCliToken === 'on') {
            const tokenScope = await question('Azure token scope [https://cognitiveservices.azure.com/.default]: ');
            if (tokenScope.trim()) {
              profile.tokenScope = tokenScope.trim();
            }
          }
        }

        if (addProfile(profile)) {
          console.log(`Profile "${name}" added successfully!`);
        } else {
          console.error('Failed to add profile');
          process.exit(1);
        }
      } catch (error) {
        console.error('Error adding profile:', error.message);
        process.exit(1);
      } finally {
        rl.close();
      }
    }
  )
  .command(
    'import-foundry',
    'Import profiles from deployed models in Foundry/Azure OpenAI accounts',
    (yargs) => {
      yargs
        .option('account', {
          type: 'string',
          describe: 'Account name to import from'
        })
        .option('resource-group', {
          type: 'string',
          describe: 'Resource group of the account'
        })
        .option('subscription', {
          type: 'string',
          describe: 'Subscription ID or name'
        })
        .option('mode', {
          type: 'string',
          choices: ['each', 'all'],
          describe: 'Prompt for each deployment or add all'
        })
        .option('all', {
          type: 'boolean',
          default: false,
          describe: 'Add all discovered deployments without prompts'
        });
    },
    async (argv) => {
      const code = await importFoundryProfiles(argv);
      process.exit(code);
    }
  )
  .command(
    'default [copilot-args..]',
    'Use the default Copilot profile',
    (yargs) => {
      yargs.positional('copilot-args', {
        describe: 'Arguments to pass to gh copilot',
        type: 'array',
        default: []
      });
    },
    async (argv) => {
      const code = await executeWithProfile('default', argv['copilot-args'] || []);
      process.exit(code);
    }
  )
  .demandCommand(1, 'You need at least one command')
  .help()
  .alias('h', 'help')
  .alias('v', 'version')
  .parse();
