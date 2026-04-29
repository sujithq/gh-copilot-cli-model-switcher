import { spawnSync } from 'child_process';
import chalk from 'chalk';
import { loadConfig, saveConfig, getProfile, buildEnvForProfile } from '../config.js';

export default {
  command: 'use <profile>',
  describe: 'Switch to a profile and launch GitHub Copilot CLI',
  builder: (yargs) =>
    yargs
      .positional('profile', {
        describe: 'Profile name to activate',
        type: 'string',
      })
      .option('dry-run', {
        type: 'boolean',
        describe: 'Print what would happen without launching copilot',
        default: false,
      }),
  handler(argv) {
    const config = loadConfig();
    const profile = getProfile(config, argv.profile);

    if (!profile) {
      console.error(
        chalk.red(`Profile "${argv.profile}" not found. Run \`copilotx list\` to see available profiles.`),
      );
      process.exit(1);
    }

    const { set, unset } = buildEnvForProfile(profile);

    // Apply to current process environment so child inherits it
    for (const key of unset) {
      delete process.env[key];
    }
    for (const [key, val] of Object.entries(set)) {
      process.env[key] = val;
    }

    // Persist last used
    config.lastUsed = profile.name;
    saveConfig(config);

    console.log(chalk.green(`✔ Switched to profile: ${chalk.bold(profile.name)}`));

    if (argv['dry-run']) {
      if (unset.length > 0) {
        console.log(chalk.gray(`  Would unset: ${unset.join(', ')}`));
      }
      for (const [key, val] of Object.entries(set)) {
        console.log(chalk.gray(`  Would set: ${key}=${val}`));
      }
      console.log(chalk.yellow('  (dry-run: copilot not launched)'));
      return;
    }

    // Try to find the copilot binary
    const copilotBin = process.env.COPILOT_BIN || 'copilot';

    const result = spawnSync(copilotBin, [], {
      stdio: 'inherit',
      env: process.env,
      shell: false,
    });

    if (result.error) {
      console.error(
        chalk.red(
          `\nFailed to launch "${copilotBin}". Is GitHub Copilot CLI installed?\n` +
            `  ${result.error.message}\n\n` +
            `Tip: use \`copilotx env ${profile.name}\` to export variables for manual use.`,
        ),
      );
      process.exit(1);
    }

    process.exit(result.status ?? 0);
  },
};
