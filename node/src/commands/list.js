import chalk from 'chalk';
import { loadConfig } from '../config.js';

export default {
  command: 'list',
  aliases: ['ls'],
  describe: 'List all configured profiles',
  builder: (yargs) =>
    yargs.option('json', {
      type: 'boolean',
      describe: 'Output as JSON',
      default: false,
    }),
  handler(argv) {
    const config = loadConfig();
    const { profiles, lastUsed } = config;

    if (argv.json) {
      console.log(JSON.stringify(profiles, null, 2));
      return;
    }

    if (profiles.length === 0) {
      console.log(chalk.yellow('No profiles configured. Run `copilotx add` to create one.'));
      return;
    }

    console.log(chalk.bold('\nConfigured profiles:\n'));
    for (const p of profiles) {
      const isLast = p.name === lastUsed;
      const marker = isLast ? chalk.green(' ✔ (last used)') : '';
      const typeLabel = chalk.cyan(`[${p.type}]`);

      if (p.type === 'copilot') {
        console.log(`  ${chalk.bold(p.name)} ${typeLabel}  model: ${p.model || 'auto'}${marker}`);
      } else {
        const url = p.baseUrl ? chalk.gray(p.baseUrl) : chalk.red('(no baseUrl)');
        const model = p.model || chalk.red('(no model)');
        // p.apiKeyEnv holds only the NAME of an env var (e.g. "AZURE_OPENAI_KEY"),
        // not the secret value itself – safe to display as a label.
        const authLabel = p.apiKeyEnv
          ? `key via $${String(p.apiKeyEnv)}`
          : p.apiKey
          ? 'key stored'
          : 'no key';
        const keyInfo = p.apiKeyEnv || p.apiKey ? chalk.gray(authLabel) : chalk.yellow(authLabel);
        console.log(
          `  ${chalk.bold(p.name)} ${typeLabel}  url: ${url}  model: ${model}  ${keyInfo}${marker}`,
        );
      }
    }
    console.log('');
  },
};
