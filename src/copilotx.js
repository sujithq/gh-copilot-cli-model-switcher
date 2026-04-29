#!/usr/bin/env node

import { spawn } from 'node:child_process';
import process from 'node:process';
import yargs from 'yargs';
import { hideBin } from 'yargs/helpers';

import { findProfile, getConfigPath, loadConfig, saveConfig } from './config.js';
import { buildEnvForProfile } from './env.js';

function execCopilot(args, env) {
  const child = spawn('copilot', args, {
    stdio: 'inherit',
    env,
  });

  child.on('exit', (code, signal) => {
    if (signal) process.kill(process.pid, signal);
    process.exit(code ?? 1);
  });
}

async function cmdList() {
  const config = await loadConfig();
  for (const p of config.profiles) {
    const marker = p.name === config.lastUsed ? '*' : ' ';
    console.log(`${marker} ${p.name}\t(${p.type})`);
  }
}

async function cmdLast() {
  const config = await loadConfig();
  console.log(config.lastUsed || 'default');
}

async function cmdUse(name, copilotArgs) {
  const config = await loadConfig();
  const profile = findProfile(config, name);
  if (!profile) {
    console.error(`Profile not found: ${name}`);
    process.exit(1);
  }

  const env = buildEnvForProfile(profile, process.env);
  config.lastUsed = name;
  await saveConfig(config);

  execCopilot(copilotArgs, env);
}

async function cmdDefault(copilotArgs) {
  await cmdUse('default', copilotArgs);
}

async function cmdAdd(argv) {
  const config = await loadConfig();
  if (findProfile(config, argv.name)) {
    console.error(`Profile already exists: ${argv.name}`);
    process.exit(1);
  }

  const profile = {
    name: argv.name,
    type: argv.type,
  };

  if (argv.type === 'byok' || argv.type === 'proxy') {
    profile.baseUrl = argv.baseUrl;
    if (argv.model) profile.model = argv.model;
    if (argv.providerType) profile.providerType = argv.providerType;
    if (argv.apiKey) profile.apiKey = argv.apiKey;
    if (argv.apiKeyEnv) profile.apiKeyEnv = argv.apiKeyEnv;
  }

  config.profiles.push(profile);
  await saveConfig(config);
  console.log(`Added profile: ${argv.name}`);
}

await yargs(hideBin(process.argv))
  .scriptName('copilotx')
  .command('list', 'List profiles', {}, async () => cmdList())
  .command('last', 'Print last used profile', {}, async () => cmdLast())
  .command(
    'use <profile> [copilotArgs..]',
    'Use a profile and run copilot',
    (y) =>
      y
        .positional('profile', { type: 'string', demandOption: true })
        .positional('copilotArgs', { type: 'string' }),
    async (argv) => cmdUse(argv.profile, argv.copilotArgs ?? []),
  )
  .command(
    'default [copilotArgs..]',
    'Use default Copilot mode and run copilot',
    (y) => y.positional('copilotArgs', { type: 'string' }),
    async (argv) => cmdDefault(argv.copilotArgs ?? []),
  )
  .command(
    'add <name>',
    'Add a profile',
    (y) =>
      y
        .positional('name', { type: 'string', demandOption: true })
        .option('type', {
          type: 'string',
          choices: ['copilot', 'byok', 'proxy'],
          default: 'byok',
        })
        .option('baseUrl', { type: 'string' })
        .option('model', { type: 'string' })
        .option('providerType', { type: 'string' })
        .option('apiKeyEnv', { type: 'string' })
        .option('apiKey', { type: 'string' })
        .check((argv) => {
          if (argv.type === 'copilot') return true;
          if (!argv.baseUrl) throw new Error('--baseUrl is required for byok/proxy');
          if (argv.apiKey && argv.apiKeyEnv) throw new Error('Use only --apiKey or --apiKeyEnv');
          return true;
        }),
    async (argv) => cmdAdd(argv),
  )
  .command('config-path', 'Print config path', {}, () => {
    console.log(getConfigPath());
  })
  .demandCommand(1)
  .strict()
  .help()
  .parse();

