import { spawnSync } from 'child_process';
import chalk from 'chalk';
import { loadConfig, saveConfig, getProfile, buildEnvForProfile } from '../config.js';

export default {
  command: 'last',
  describe: 'Show the last used profile, or re-activate it',
  builder: (yargs) =>
    yargs.option('use', {
      type: 'boolean',
      describe: 'Re-activate the last used profile (exec copilot)',
      default: false,
    }),
  handler(argv) {
    const config = loadConfig();
    const { lastUsed } = config;

    if (!lastUsed) {
      console.log(chalk.yellow('No profile has been used yet. Run `copilotx use <profile>` first.'));
      return;
    }

    const profile = getProfile(config, lastUsed);

    if (!profile) {
      console.log(chalk.yellow(`Last used profile "${lastUsed}" no longer exists.`));
      return;
    }

    if (!argv.use) {
      console.log(chalk.bold(`\nLast used profile: ${chalk.green(lastUsed)}\n`));
      console.log(`  Type:  ${chalk.cyan(profile.type)}`);
      if (profile.type === 'copilot') {
        console.log(`  Model: ${profile.model || 'auto'}`);
      } else {
        console.log(`  URL:   ${chalk.gray(profile.baseUrl || '(none)')}`);
        console.log(`  Model: ${profile.model || '(none)'}`);
      }
      console.log('');
      console.log(chalk.gray(`  Run \`copilotx last --use\` to re-activate it.`));
      console.log('');
      return;
    }

    // --use: re-activate the last profile
    const { set, unset } = buildEnvForProfile(profile);
    for (const key of unset) delete process.env[key];
    for (const [key, val] of Object.entries(set)) process.env[key] = val;

    // Save again so timestamp is refreshed (no-op on lastUsed field)
    config.lastUsed = profile.name;
    saveConfig(config);

    console.log(chalk.green(`✔ Re-activating profile: ${chalk.bold(profile.name)}`));

    const copilotBin = process.env.COPILOT_BIN || 'copilot';
    const result = spawnSync(copilotBin, [], { stdio: 'inherit', env: process.env });

    if (result.error) {
      console.error(chalk.red(`Failed to launch "${copilotBin}": ${result.error.message}`));
      process.exit(1);
    }
    process.exit(result.status ?? 0);
  },
};
