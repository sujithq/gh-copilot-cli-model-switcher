import inquirer from 'inquirer';
import chalk from 'chalk';
import { loadConfig, saveConfig, getProfile } from '../config.js';

const PROFILE_TYPES = ['copilot', 'byok'];

export default {
  command: 'add [name]',
  describe: 'Interactively add a new model profile',
  builder: (yargs) =>
    yargs.positional('name', {
      describe: 'Profile name (will be prompted if omitted)',
      type: 'string',
    }),
  async handler(argv) {
    const config = loadConfig();

    const answers = await inquirer.prompt([
      {
        type: 'input',
        name: 'name',
        message: 'Profile name:',
        default: argv.name || undefined,
        when: !argv.name,
        validate: (val) => {
          if (!val.trim()) return 'Name cannot be empty.';
          if (getProfile(config, val.trim())) return `Profile "${val.trim()}" already exists.`;
          return true;
        },
      },
      {
        type: 'list',
        name: 'type',
        message: 'Profile type:',
        choices: PROFILE_TYPES,
        default: 'copilot',
      },
      {
        type: 'input',
        name: 'model',
        message: 'Model name (e.g. gpt-4o, llama3):',
        default: 'auto',
        when: (ans) => ans.type === 'copilot',
      },
      {
        type: 'input',
        name: 'baseUrl',
        message: 'Provider base URL (e.g. https://xxx.openai.azure.com/openai/deployments/gpt):',
        when: (ans) => ans.type === 'byok',
        validate: (val) => (val.trim() ? true : 'Base URL cannot be empty.'),
      },
      {
        type: 'input',
        name: 'model',
        message: 'Model name:',
        when: (ans) => ans.type === 'byok',
        validate: (val) => (val.trim() ? true : 'Model cannot be empty.'),
      },
      {
        type: 'list',
        name: 'authMethod',
        message: 'API key method:',
        choices: ['environment variable', 'inline value', 'none'],
        when: (ans) => ans.type === 'byok',
      },
      {
        type: 'input',
        name: 'apiKeyEnv',
        message: 'Environment variable name that holds the API key (e.g. AZURE_OPENAI_KEY):',
        when: (ans) => ans.type === 'byok' && ans.authMethod === 'environment variable',
        validate: (val) => (val.trim() ? true : 'Variable name cannot be empty.'),
      },
      {
        type: 'password',
        name: 'apiKey',
        message: 'API key value (stored in plain text in ~/.copilotx/config.json):',
        when: (ans) => ans.type === 'byok' && ans.authMethod === 'inline value',
        validate: (val) => (val.trim() ? true : 'API key cannot be empty.'),
      },
      {
        type: 'input',
        name: 'providerType',
        message: 'Provider type (optional, e.g. azure, openai):',
        default: '',
        when: (ans) => ans.type === 'byok',
      },
    ]);

    const profileName = argv.name || answers.name;

    // Validate name when passed as positional arg
    if (argv.name) {
      if (!argv.name.trim()) {
        console.error(chalk.red('Profile name cannot be empty.'));
        process.exit(1);
      }
      if (getProfile(config, argv.name.trim())) {
        console.error(chalk.red(`Profile "${argv.name.trim()}" already exists.`));
        process.exit(1);
      }
    }

    const newProfile = { name: profileName.trim(), type: answers.type };

    if (answers.type === 'copilot') {
      newProfile.model = answers.model || 'auto';
    } else {
      newProfile.baseUrl = answers.baseUrl.trim();
      newProfile.model = answers.model.trim();
      if (answers.apiKeyEnv) newProfile.apiKeyEnv = answers.apiKeyEnv.trim();
      if (answers.apiKey) newProfile.apiKey = answers.apiKey.trim();
      if (answers.providerType && answers.providerType.trim()) {
        newProfile.providerType = answers.providerType.trim();
      }
    }

    config.profiles.push(newProfile);
    saveConfig(config);

    console.log(chalk.green(`\n✔ Profile "${newProfile.name}" added successfully.`));
    console.log(chalk.gray(`  Run \`copilotx use ${newProfile.name}\` to activate it.\n`));
  },
};
