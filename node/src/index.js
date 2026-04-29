#!/usr/bin/env node
import yargs from 'yargs';
import { hideBin } from 'yargs/helpers';

import listCmd from './commands/list.js';
import useCmd from './commands/use.js';
import addCmd from './commands/add.js';
import lastCmd from './commands/last.js';
import defaultCmd from './commands/default.js';
import envCmd from './commands/env.js';
import removeCmd from './commands/remove.js';

yargs(hideBin(process.argv))
  .scriptName('copilotx')
  .usage('$0 <command> [options]')
  .command(listCmd)
  .command(useCmd)
  .command(addCmd)
  .command(lastCmd)
  .command(defaultCmd)
  .command(envCmd)
  .command(removeCmd)
  .demandCommand(1, 'You must provide a command. Run --help to see available commands.')
  .strict()
  .help()
  .alias('h', 'help')
  .version()
  .alias('v', 'version')
  .epilog('For more information visit https://github.com/sujithq/gh-copilot-cli-model-switcher')
  .parse();
