import chalk from 'chalk';
import { loadConfig, getProfile, buildEnvForProfile } from '../config.js';

export default {
  command: 'env <profile>',
  describe: 'Print shell export commands for a profile (eval-friendly)',
  builder: (yargs) =>
    yargs
      .positional('profile', {
        describe: 'Profile name',
        type: 'string',
      })
      .option('shell', {
        type: 'string',
        describe: 'Shell syntax: bash, fish, powershell',
        choices: ['bash', 'fish', 'powershell'],
        default: 'bash',
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

    const { set, unset } = buildEnvForProfile(profile);
    const shell = argv.shell;

    if (shell === 'fish') {
      for (const key of unset) {
        console.log(`set -e ${key}`);
      }
      for (const [key, val] of Object.entries(set)) {
        console.log(`set -x ${key} '${val}'`);
      }
    } else if (shell === 'powershell') {
      for (const key of unset) {
        console.log(`Remove-Item Env:${key} -ErrorAction SilentlyContinue`);
      }
      for (const [key, val] of Object.entries(set)) {
        console.log(`$Env:${key} = '${val}'`);
      }
    } else {
      // bash / sh (default)
      for (const key of unset) {
        console.log(`unset ${key}`);
      }
      for (const [key, val] of Object.entries(set)) {
        console.log(`export ${key}='${val}'`);
      }
    }
  },
};
