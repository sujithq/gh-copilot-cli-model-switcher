import chalk from 'chalk';
import { loadConfig, saveConfig, getProfile } from '../config.js';

export default {
  command: 'default [profile]',
  describe: 'Show or set the default profile (used when no profile is specified)',
  builder: (yargs) =>
    yargs.positional('profile', {
      describe: 'Profile name to set as default',
      type: 'string',
    }),
  handler(argv) {
    const config = loadConfig();

    if (!argv.profile) {
      // Show current default
      const def = config.defaultProfile || 'default';
      console.log(chalk.bold(`\nDefault profile: ${chalk.green(def)}\n`));
      const p = getProfile(config, def);
      if (p) {
        console.log(`  Type:  ${chalk.cyan(p.type)}`);
        if (p.type === 'copilot') {
          console.log(`  Model: ${p.model || 'auto'}`);
        } else {
          console.log(`  URL:   ${chalk.gray(p.baseUrl || '(none)')}`);
          console.log(`  Model: ${p.model || '(none)'}`);
        }
      } else {
        console.log(chalk.yellow(`  Profile "${def}" not found in config.`));
      }
      console.log('');
      return;
    }

    const profile = getProfile(config, argv.profile);
    if (!profile) {
      console.error(
        chalk.red(
          `Profile "${argv.profile}" not found. Run \`copilotx list\` to see available profiles.`,
        ),
      );
      process.exit(1);
    }

    config.defaultProfile = argv.profile;
    saveConfig(config);
    console.log(chalk.green(`✔ Default profile set to: ${chalk.bold(argv.profile)}`));
  },
};
