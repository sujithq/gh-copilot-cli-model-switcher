import chalk from 'chalk';
import { loadConfig, saveConfig, getProfile } from '../config.js';

export default {
  command: 'remove <profile>',
  aliases: ['rm', 'delete'],
  describe: 'Remove a profile from the configuration',
  builder: (yargs) =>
    yargs.positional('profile', {
      describe: 'Profile name to remove',
      type: 'string',
    }),
  handler(argv) {
    const config = loadConfig();
    const profile = getProfile(config, argv.profile);

    if (!profile) {
      console.error(
        chalk.red(
          `Profile "${argv.profile}" not found. Run \`copilotx list\` to see available profiles.`,
        ),
      );
      process.exit(1);
    }

    config.profiles = config.profiles.filter((p) => p.name !== argv.profile);

    if (config.lastUsed === argv.profile) {
      config.lastUsed = config.profiles[0]?.name || null;
    }
    if (config.defaultProfile === argv.profile) {
      config.defaultProfile = config.profiles[0]?.name || null;
    }

    saveConfig(config);
    console.log(chalk.green(`✔ Profile "${argv.profile}" removed.`));
  },
};
