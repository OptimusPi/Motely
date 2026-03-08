#!/usr/bin/env node
import { cpSync, existsSync, rmSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const browserFrameworkSource = join(__dirname, 'Motely.BrowserWasm', 'bin', 'Release', 'net10.0-browser', 'publish', 'wwwroot', '_framework');
const singleThreadFrameworkSource = join(__dirname, 'Motely.SingleThread', 'bin', 'Release', 'net10.0-browser', 'publish', 'wwwroot', '_framework');

const targets = {
  browser: [
    { source: browserFrameworkSource, destination: join(__dirname, 'Motely.npm', '_framework') },
  ],
  singlethread: [
    { source: singleThreadFrameworkSource, destination: join(__dirname, 'Motely.npm', '_framework_st') },
    { source: singleThreadFrameworkSource, destination: join(__dirname, 'Motely.npm.singlethread', '_framework') },
  ],
  node: [
    { source: singleThreadFrameworkSource, destination: join(__dirname, 'Motely.node', '_framework') },
  ],
};

const modes = process.argv.slice(2);
const selectedModes = modes.length === 0 || modes.includes('all')
  ? ['browser', 'singlethread', 'node']
  : modes;

const shouldInclude = (path) => {
  const normalized = path.replace(/\\/g, '/');
  return !normalized.endsWith('.br')
    && !normalized.endsWith('.gz')
    && !normalized.includes('/debug/')
    && !normalized.includes('/tmp/');
};

for (const mode of selectedModes) {
  if (!(mode in targets)) {
    console.error(`Unknown staging target: ${mode}`);
    process.exit(1);
  }

  for (const { source, destination } of targets[mode]) {
    if (!existsSync(source)) {
      console.error(`Missing publish output for ${mode}: ${source}`);
      process.exit(1);
    }

    rmSync(destination, { recursive: true, force: true });
    cpSync(source, destination, {
      recursive: true,
      force: true,
      filter: (src) => shouldInclude(src),
    });
    console.log(`${mode}: ${source} -> ${destination}`);
  }
}
