#!/usr/bin/env node

const yargs = require('yargs/yargs');
const { hideBin } = require('yargs/helpers');
const { spawn, spawnSync } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');
const readline = require('readline');
const {
  sanitizeProfilePart,
  isApplicableAccount,
  isChatCapableDeployment,
  mapDeployment,
  buildImportedProfile
} = require('./foundry');
const {
  getProfile,
  addProfile,
  upsertProfile,
  listProfiles,
  removeProfiles,
  setLastUsed,
  getLastUsed,
  getConfigFile
} = require('./config');

function promptForSelection(message, maxOption) {
  return new Promise((resolve) => {
    const rl = readline.createInterface({
      input: process.stdin,
      output: process.stdout
    });

    rl.question(message, (answer) => {
      rl.close();
      const selection = parseInt(answer, 10);
      if (!isNaN(selection) && selection > 0 && selection <= maxOption) {
        resolve(selection);
      } else {
        resolve(null);
      }
    });
  });
}

function isAzureProfile(profile) {
  const baseUrl = (profile.baseUrl || '').toLowerCase();
  const providerType = (profile.providerType || '').toLowerCase();
  return baseUrl.includes('.openai.azure.com') || providerType === 'azure';
}

function getAzureDeploymentFromBaseUrl(baseUrl) {
  const value = baseUrl || '';
  const m = value.match(/\/openai\/deployments\/([^/?#]+)/i);
  return m ? decodeURIComponent(m[1]) : '';
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
    delete process.env.COPILOT_PROVIDER_BEARER_TOKEN;
    delete process.env.COPILOT_MODEL;
    delete process.env.COPILOT_PROVIDER_TYPE;
    return { usedAzureCliToken: false };
  } else if (profile.type === 'byok' || profile.type === 'proxy') {
    if (profile.baseUrl) {
      process.env.COPILOT_PROVIDER_BASE_URL = profile.baseUrl;
    }

    if (profile.model) {
      let modelForProvider = profile.model;
      if (isAzureProfile(profile)) {
        const deploymentName = getAzureDeploymentFromBaseUrl(profile.baseUrl);
        if (deploymentName && modelForProvider.toLowerCase() !== deploymentName.toLowerCase()) {
          // Azure BYOK providers expect the deployment identifier as COPILOT_MODEL.
          modelForProvider = deploymentName;
          console.log(`Using Azure deployment name '${deploymentName}' as model for provider compatibility.`);
        }
      }
      process.env.COPILOT_MODEL = modelForProvider;
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
      delete process.env.COPILOT_PROVIDER_API_KEY;
      process.env.COPILOT_PROVIDER_BEARER_TOKEN = token;
    } else if (resolvedApiKey) {
      process.env.COPILOT_PROVIDER_API_KEY = resolvedApiKey;
      delete process.env.COPILOT_PROVIDER_BEARER_TOKEN;
    } else {
      delete process.env.COPILOT_PROVIDER_API_KEY;
      delete process.env.COPILOT_PROVIDER_BEARER_TOKEN;
    }

    if (profile.providerType) {
      process.env.COPILOT_PROVIDER_TYPE = profile.providerType;
    }

    return { usedAzureCliToken: useAzureCliToken };
  }

  return { usedAzureCliToken: false };
}

const DEFAULT_MCP_COMPAT_SERVERS = ['foundry-mcp', 'context7', 'msx-mcp', 'azure', 'workiq', 'powerbi-remote'];

function getMcpConfigPaths() {
  const home = os.homedir();
  const candidates = [];
  const seen = new Set();

  const add = (p) => {
    const normalized = path.normalize(p);
    if (!seen.has(normalized)) {
      seen.add(normalized);
      candidates.push(normalized);
    }
  };

  // 1) Project/workspace-level config first (walk up from current directory)
  let dir = process.cwd();
  while (true) {
    add(path.join(dir, 'mcp.json'));
    add(path.join(dir, '.vscode', 'mcp.json'));
    add(path.join(dir, '.vscode', 'settings.json'));

    const parent = path.dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }

  // 2) User-level config next
  if (process.platform === 'win32') {
    const appData = process.env.APPDATA || home;
    add(path.join(appData, 'Code', 'User', 'settings.json'));
    add(path.join(appData, 'Code', 'User', 'mcp.json'));
  } else if (process.platform === 'darwin') {
    const base = path.join(home, 'Library', 'Application Support', 'Code', 'User');
    add(path.join(base, 'settings.json'));
    add(path.join(base, 'mcp.json'));
  } else {
    const base = path.join(home, '.config', 'Code', 'User');
    add(path.join(base, 'settings.json'));
    add(path.join(base, 'mcp.json'));
  }

  return candidates;
}

function discoverMcpServers() {
  const discovered = new Set();

  // Strategy 1: Project-level then user-level settings.json / mcp.json
  try {
    for (const p of getMcpConfigPaths()) {
      if (fs.existsSync(p)) {
        const data = JSON.parse(fs.readFileSync(p, 'utf8'));
        // settings.json: "mcp": { "servers": { "name": {...} } }
        // mcp.json:      { "servers": { "name": {...} } }
        const servers = (data && data.mcp && data.mcp.servers)
          || (data && data.servers)
          || {};
        Object.keys(servers).forEach((name) => discovered.add(name));
      }
    }
  } catch { /* ignore read/parse errors */ }

  // Strategy 2: gh config list — look for copilot.mcp* entries
  try {
    const r = spawnSync('gh', ['config', 'list'], {
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe'],
      timeout: 5000
    });
    if (r.status === 0 && r.stdout) {
      for (const line of r.stdout.split('\n')) {
        // e.g. "copilot.mcp_servers.foundry-mcp.url = ..."
        const m = line.trim().match(/^copilot\.mcp[_-]?servers?\.([^.\s]+)/i);
        if (m) discovered.add(m[1]);
      }
    }
  } catch { /* gh not found or error */ }

  return [...discovered];
}

function isTokenFailure(text) {
  return /(401|unauthorized|forbidden|invalid token|token expired|expired token|authentication failed|permission denied)/i.test(text || '');
}

async function promptMcpCompatServers(previousSelection) {
  const discovered = discoverMcpServers();
  const candidates = discovered.length > 0 ? discovered : DEFAULT_MCP_COMPAT_SERVERS;
  const isDiscovered = discovered.length > 0;

  // Default selection: all candidates (disable everything for maximum compat)
  const prev = previousSelection ?? candidates;
  const prevLabel = prev.length === candidates.length
    ? 'all'
    : prev.length === 0
      ? 'none'
      : prev.join(', ');

  console.log('\nSelect MCP servers to disable for Azure BYOK compat mode.');
  if (isDiscovered) {
    console.log(`Discovered ${candidates.length} configured MCP server(s):`);
  } else {
    console.log('Could not auto-discover MCP servers. Using known candidates:');
  }
  candidates.forEach((s, i) => {
    const checked = prev.includes(s) ? 'x' : ' ';
    console.log(`  ${i + 1}. [${checked}] ${s}`);
  });
  console.log(`\nEnter numbers to disable (space/comma-separated), 'all', or 'none'.`);

  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  const answer = await new Promise((resolve) => {
    rl.question(`Selection [${prevLabel}]: `, (ans) => { rl.close(); resolve((ans || '').trim()); });
  });

  if (!answer) return prev;
  if (answer.toLowerCase() === 'all') return [...candidates];
  if (answer.toLowerCase() === 'none') return [];

  const nums = answer.split(/[\s,]+/).map((n) => parseInt(n, 10)).filter((n) => !isNaN(n) && n >= 1 && n <= candidates.length);
  if (nums.length === 0) return prev;
  return nums.map((n) => candidates[n - 1]);
}

function buildCopilotArgs(profile, copilotArgs = []) {
  const disableCompat = (process.env.COPILOTX_DISABLE_MCP_COMPAT || '').trim().toLowerCase() === 'off';
  const args = [...copilotArgs];

  const hasPromptMode = args.includes('-p') || args.includes('--prompt');
  const hasPermissionControls = args.includes('--allow-all-tools')
    || args.includes('--allow-all')
    || args.includes('--yolo')
    || args.includes('--allow-tool');

  // In non-interactive prompt mode, allow tools automatically so CLI doesn't fail
  // with "could not request permission from user".
  if (hasPromptMode && !hasPermissionControls) {
    args.push('--allow-all-tools');
  }

  // Azure BYOK providers can exceed tool-count limits when many MCP servers are present.
  if (!disableCompat && (profile.type === 'byok' || profile.type === 'proxy') && isAzureProfile(profile)) {
    const hasManualMcpControls = args.some((a) =>
      a === '--disable-mcp-server' || a === '--disable-builtin-mcps' || a === '--available-tools'
    );

    if (!hasManualMcpControls) {
      const serversToDisable = profile.mcpCompatServers ?? DEFAULT_MCP_COMPAT_SERVERS;
      const mcpArgs = serversToDisable.flatMap((s) => ['--disable-mcp-server', s]);
      console.log('Applying Azure BYOK MCP compatibility mode to avoid provider tool-limit errors.');
      return [
        '--disable-builtin-mcps',
        ...mcpArgs,
        ...args
      ];
    }
  }

  return args;
}

function askQuestion(rl, prompt) {
  return new Promise((resolve) => rl.question(prompt, resolve));
}

async function promptProfilesToRemove(profiles) {
  if (!profiles.length) {
    return [];
  }

  console.log('\nSelect profiles to remove:');
  profiles.forEach((p, i) => {
    console.log(`  ${i + 1}. ${p.name} (${p.type})`);
  });
  console.log(`\nEnter numbers to remove (space/comma-separated), 'all', or 'none'.`);

  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  const answer = await new Promise((resolve) => {
    rl.question('Selection [none]: ', (ans) => { rl.close(); resolve((ans || '').trim()); });
  });

  if (!answer || answer.toLowerCase() === 'none') return [];
  if (answer.toLowerCase() === 'all') return profiles.map((p) => p.name);

  const nums = answer.split(/[\s,]+/).map((n) => parseInt(n, 10)).filter((n) => !isNaN(n) && n >= 1 && n <= profiles.length);
  if (nums.length === 0) return [];

  return [...new Set(nums.map((n) => profiles[n - 1].name))];
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
  const args = [
    'cognitiveservices',
    'account',
    'list',
    '--query',
    '[].{name:name,resourceGroup:resourceGroup,kind:kind,endpoint:properties.endpoint}',
    '-o',
    'json'
  ];
  if (subscription) {
    args.push('--subscription', subscription);
  }

  const accounts = await runAzJson(args);
  return accounts.filter(isApplicableAccount);
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
  return deployments
    .filter(isChatCapableDeployment)
    .map(mapDeployment)
    .filter((item) => item.deploymentName);
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
    const existingNames = new Set(listProfiles().map((profile) => profile.name));

    for (const account of accounts) {
      const accountName = account.name;
      const resourceGroup = account.resourceGroup;
      const endpoint = ((account.endpoint) || `https://${accountName}.openai.azure.com`).replace(/\/$/, '');

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

        const profile = buildImportedProfile(accountName, endpoint, dep, existingNames);
        const result = upsertProfile(profile);

        if (!result.ok) {
          console.warn(`  Failed to add profile: ${profile.name}`);
          continue;
        }

        existingNames.add(result.name);
        if (result.action === 'added') {
          importedCount += 1;
          console.log(`  Added profile: ${result.name}`);
        } else if (result.action === 'updated-equivalent') {
          console.log(`  Reused existing equivalent profile: ${result.name}`);
        } else {
          console.log(`  Updated profile: ${result.name}`);
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

function runCopilot(copilotArgs, interactiveMode = false) {
  return new Promise((resolve, reject) => {
    const ghArgs = copilotArgs.length > 0 ? ['copilot', '--', ...copilotArgs] : ['copilot'];

    if (interactiveMode) {
      const copilot = spawn('gh', ghArgs, {
        stdio: 'inherit',
        env: process.env
      });

      copilot.on('error', (error) => {
        reject(error);
      });

      copilot.on('close', (code) => {
        resolve({ code: code || 0, output: '' });
      });

      return;
    }

    const copilot = spawn('gh', ghArgs, {
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

  const userRequestedInteractive = copilotArgs.length === 0;

  // For Azure BYOK profiles in interactive mode, prompt for MCP servers to disable on first use.
  const needsCompat = !((process.env.COPILOTX_DISABLE_MCP_COMPAT || '').trim().toLowerCase() === 'off')
    && (profile.type === 'byok' || profile.type === 'proxy') && isAzureProfile(profile);
  const hasManualMcpControls = copilotArgs.some((a) =>
    a === '--disable-mcp-server' || a === '--disable-builtin-mcps' || a === '--available-tools'
  );

  if (needsCompat && !hasManualMcpControls && userRequestedInteractive && profile.mcpCompatServers === undefined) {
    profile.mcpCompatServers = await promptMcpCompatServers(undefined);
    addProfile(profile);
  }

  const effectiveCopilotArgs = buildCopilotArgs(profile, copilotArgs);

  // If no args provided, show a hint that we're entering interactive mode
  if (userRequestedInteractive) {
    console.log('Launching gh copilot in interactive mode. Type your question below:');
    console.log('If prompted to trust this folder, choose option 2 once to remember it.\n');
  }

  try {
    let result = await runCopilot(effectiveCopilotArgs, userRequestedInteractive);

    if (result.code !== 0 && envInfo.usedAzureCliToken && isTokenFailure(result.output)) {
      console.warn('Detected token-related auth failure. Refreshing Azure CLI token and retrying once...');
      const refreshedToken = await getAzureCliToken(profile);
      delete process.env.COPILOT_PROVIDER_API_KEY;
      process.env.COPILOT_PROVIDER_BEARER_TOKEN = refreshedToken;
      result = await runCopilot(effectiveCopilotArgs, userRequestedInteractive);
    }

    return result.code;
  } catch (error) {
    console.error('Error executing gh copilot:', error.message);
    console.error('Make sure GitHub Copilot CLI is installed: gh extension install github/gh-copilot');
    return 1;
  }
}

async function runAddProfileWizard() {
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
      return 1;
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

    const result = upsertProfile(profile);
    if (result.ok) {
      if (result.action === 'added') {
        console.log(`Profile "${result.name}" added successfully!`);
      } else if (result.action === 'updated-equivalent') {
        console.log(`Equivalent profile already existed; updated "${result.name}" instead of creating a duplicate.`);
      } else {
        console.log(`Profile "${result.name}" updated successfully!`);
      }
      return 0;
    }

    console.error('Failed to add profile');
    return 1;
  } catch (error) {
    console.error('Error adding profile:', error.message);
    return 1;
  } finally {
    rl.close();
  }
}

const argv = yargs(hideBin(process.argv))
  .scriptName('copilotx')
  .usage('$0 <command> [options]')
  .command(
    'list',
    'List all available profiles',
    (yargs) => {
      yargs.example('$0 list', 'Show all profiles (read-only)');
    },
    async (argv) => {
      const profiles = listProfiles();
      const lastUsed = getLastUsed();

      console.log('Available profiles:');
      console.log('');

      profiles.forEach((profile, index) => {
        const marker = profile.name === lastUsed ? '* ' : '  ';
        console.log(`${index + 1}. ${marker}${profile.name} (${profile.type})`);

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
    'manage',
    'Interactive profile management (Use/Remove/Add/Import in one flow)',
    (yargs) => {
      yargs.example('$0 manage', 'Choose Use, Remove, Add, or Import from one interactive screen');
    },
    async () => {
      const profiles = listProfiles();
      const lastUsed = getLastUsed();

      if (!profiles.length) {
        console.log('No profiles found.');
        process.exit(0);
      }

      console.log('Available profiles:');
      console.log('');
      profiles.forEach((profile, index) => {
        const marker = profile.name === lastUsed ? '* ' : '  ';
        console.log(`${index + 1}. ${marker}${profile.name} (${profile.type})`);
      });

      const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
      const answer = await new Promise((resolve) => {
        rl.question('\nAction: [u]se / [r]emove / [a]dd / [i]mport / [Enter] exit: ', (ans) => resolve((ans || '').trim().toLowerCase()));
      });

      if (!answer) {
        rl.close();
        process.exit(0);
      }

      if (answer === 'u' || answer === 'use') {
        const selectionRaw = await new Promise((resolve) => {
          rl.question('Profile #: ', (ans) => resolve((ans || '').trim()));
        });

        const selection = parseInt(selectionRaw, 10);
        rl.close();

        if (!isNaN(selection) && selection >= 1 && selection <= profiles.length) {
          const selectedProfile = profiles[selection - 1];
          const code = await executeWithProfile(selectedProfile.name, []);
          process.exit(code);
          return;
        }

        console.log('Invalid selection.');
        process.exit(1);
        return;
      }

      if (answer === 'r' || answer === 'remove') {
        rl.close();
        const removable = profiles.filter((p) => p.name.toLowerCase() !== 'default');
        const targets = await promptProfilesToRemove(removable);

        if (!targets.length) {
          console.log('No profiles selected.');
          process.exit(0);
        }

        const result = removeProfiles(targets);
        if (!result.ok) {
          console.error('Failed to remove profiles.');
          process.exit(1);
          return;
        }

        console.log(`Removed ${result.removed} profile(s).`);
        process.exit(0);
        return;
      }

      if (answer === 'a' || answer === 'add') {
        rl.close();
        const code = await runAddProfileWizard();
        process.exit(code);
        return;
      }

      if (answer === 'i' || answer === 'import') {
        rl.close();
        const code = await importFoundryProfiles({});
        process.exit(code);
        return;
      }

      rl.close();
      console.log('Unknown action.');
      process.exit(1);
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
        })
        .example('$0 use azure-gpt', 'Interactive mode with the azure-gpt profile')
        .example('$0 use azure-gpt suggest "how to list files"', 'Pass a sub-command to gh copilot')
        .example('$0 use azure-gpt -p "fix the failing tests"', 'Non-interactive prompt mode')
        .example('$0 use azure-gpt -p "refactor this" --allow-tool=write', 'Restrict to the write tool only')
        .example('$0 use azure-gpt -p "explain this" --deny-tool=run_command', 'Deny a specific tool');
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
      yargs
        .positional('copilot-args', {
          describe: 'Arguments to pass to gh copilot',
          type: 'array',
          default: []
        })
        .example('$0 last', 'Interactive mode with the last used profile')
        .example('$0 last -p "explain this code"', 'Non-interactive prompt mode')
        .example('$0 last suggest "how to list files"', 'Pass a sub-command to gh copilot');
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
    (yargs) => {
      yargs.example('$0 add', 'Start the interactive wizard to add or update a profile');
    },
    async () => {
      const code = await runAddProfileWizard();
      process.exit(code);
    }
  )
  .command(
    'remove [profiles..]',
    'Remove one or more profiles (interactive multi-select when omitted)'
    , (yargs) => {
      yargs
        .positional('profiles', {
          describe: 'Profile names to remove',
          type: 'array',
          default: []
        })
        .example('$0 remove azure-gpt ollama-local', 'Remove multiple profiles by name')
        .example('$0 remove', 'Interactively select multiple profiles to remove');
    },
    async (argv) => {
      let targets = (argv.profiles || []).map((n) => `${n}`.trim()).filter(Boolean);

      if (!targets.length) {
        const removable = listProfiles().filter((p) => p.name.toLowerCase() !== 'default');
        targets = await promptProfilesToRemove(removable);
      }

      if (!targets.length) {
        console.log('No profiles selected.');
        process.exit(0);
      }

      const result = removeProfiles(targets);
      if (!result.ok) {
        console.error('Failed to remove profiles.');
        process.exit(1);
      }

      console.log(`Removed ${result.removed} profile(s).`);
      process.exit(0);
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
        })
        .example('$0 import-foundry', 'Discover all accounts and prompt per deployment')
        .example('$0 import-foundry --all', 'Import all discovered deployments without prompts')
        .example('$0 import-foundry --mode each', 'Explicitly prompt for each deployment')
        .example('$0 import-foundry --account myfoundry --resource-group my-rg --all', 'Target one specific account')
        .example('$0 import-foundry --subscription 00000000-0000-0000-0000-000000000000 --all', 'Scope to a specific subscription');
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
      yargs
        .positional('copilot-args', {
          describe: 'Arguments to pass to gh copilot',
          type: 'array',
          default: []
        })
        .example('$0 default', 'Interactive mode with the default Copilot profile')
        .example('$0 default -p "explain this code"', 'Non-interactive prompt mode')
        .example('$0 default suggest "how to list files"', 'Pass a sub-command to gh copilot');
    },
    async (argv) => {
      const code = await executeWithProfile('default', argv['copilot-args'] || []);
      process.exit(code);
    }
  )
  .example('$0 list', 'List all profiles; select a number to launch one interactively')
  .example('$0 manage', 'Use or remove profiles from one interactive flow')
  .example('$0 remove', 'Interactively select profiles to remove')
  .example('$0 use azure-gpt -p "fix the tests"', 'Run a prompt with a specific profile')
  .example('$0 last', 'Use the most recently used profile interactively')
  .example('$0 import-foundry --all', 'Import all Foundry deployments as profiles')
  .demandCommand(1, 'You need at least one command')
  .help()
  .alias('h', 'help')
  .alias('v', 'version')
  .parse();
