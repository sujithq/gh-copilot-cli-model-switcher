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
  CONFIG_FILE
} = require('./config');

function setEnvironmentForProfile(profile) {
  if (profile.type === 'copilot') {
    delete process.env.COPILOT_PROVIDER_BASE_URL;
    delete process.env.COPILOT_PROVIDER_API_KEY;
    delete process.env.COPILOT_MODEL;
    delete process.env.COPILOT_PROVIDER_TYPE;
  } else if (profile.type === 'byok' || profile.type === 'proxy') {
    if (profile.baseUrl) {
      process.env.COPILOT_PROVIDER_BASE_URL = profile.baseUrl;
    }

    if (profile.model) {
      process.env.COPILOT_MODEL = profile.model;
    }

    if (profile.apiKeyEnv) {
      const apiKey = process.env[profile.apiKeyEnv];
      if (apiKey) {
        process.env.COPILOT_PROVIDER_API_KEY = apiKey;
      } else {
        console.warn(`Warning: Environment variable ${profile.apiKeyEnv} is not set`);
      }
    } else if (profile.apiKey) {
      process.env.COPILOT_PROVIDER_API_KEY = profile.apiKey;
    }

    if (profile.providerType) {
      process.env.COPILOT_PROVIDER_TYPE = profile.providerType;
    }
  }
}

function executeWithProfile(profileName, copilotArgs = []) {
  const profile = getProfile(profileName);

  if (!profile) {
    console.error(`Profile "${profileName}" not found.`);
    console.error('Use "copilotx list" to see available profiles.');
    process.exit(1);
  }

  console.log(`Using profile: ${profile.name} (${profile.type})`);

  setEnvironmentForProfile(profile);
  setLastUsed(profileName);

  const copilot = spawn('gh', ['copilot', ...copilotArgs], {
    stdio: 'inherit',
    env: process.env
  });

  copilot.on('error', (error) => {
    console.error('Error executing gh copilot:', error.message);
    console.error('Make sure GitHub Copilot CLI is installed: gh extension install github/gh-copilot');
    process.exit(1);
  });

  copilot.on('close', (code) => {
    process.exit(code || 0);
  });
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
      console.log(`\nConfig file: ${CONFIG_FILE}`);
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
    (argv) => {
      executeWithProfile(argv.profile, argv['copilot-args'] || []);
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
    (argv) => {
      const lastUsed = getLastUsed();
      if (!lastUsed) {
        console.error('No profile has been used yet.');
        process.exit(1);
      }
      executeWithProfile(lastUsed, argv['copilot-args'] || []);
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
    'default [copilot-args..]',
    'Use the default Copilot profile',
    (yargs) => {
      yargs.positional('copilot-args', {
        describe: 'Arguments to pass to gh copilot',
        type: 'array',
        default: []
      });
    },
    (argv) => {
      executeWithProfile('default', argv['copilot-args'] || []);
    }
  )
  .demandCommand(1, 'You need at least one command')
  .help()
  .alias('h', 'help')
  .alias('v', 'version')
  .parse();
