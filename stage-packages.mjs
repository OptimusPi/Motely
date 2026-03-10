#!/usr/bin/env node
import { cpSync, existsSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const browserFrameworkSource = join(__dirname, 'Motely.BrowserWasm', 'bin', 'Release', 'net10.0-browser', 'publish', 'wwwroot', '_framework');
const singleThreadFrameworkSource = join(__dirname, 'Motely.SingleThread', 'bin', 'Release', 'net10.0-browser', 'publish', 'wwwroot', '_framework');
const nodeAddonSource = join(__dirname, 'Motely.NodeAddon', 'bin', 'Release', 'net10.0');

const targets = {
  browser: [
    { source: browserFrameworkSource, destination: join(__dirname, 'Motely.npm', '_framework') },
  ],
  singlethread: [
    { source: singleThreadFrameworkSource, destination: join(__dirname, 'Motely.npm', '_framework_st') },
    { source: singleThreadFrameworkSource, destination: join(__dirname, 'Motely.npm.singlethread', '_framework') },
  ],
  node: [
    { source: nodeAddonSource, destination: join(__dirname, 'Motely.node', 'addon') },
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

function patchNodeAddonLoaders(destination) {
  const files = ['Motely.NodeAddon.mjs', 'Motely.NodeAddon.cjs'];
  for (const file of files) {
    const fullPath = join(destination, file);
    if (!existsSync(fullPath)) continue;
    const original = readFileSync(fullPath, 'utf8');
    const patched = original.replaceAll('node-api-dotnet/net10.0', 'node-api-dotnet/net9.0');
    if (patched !== original) {
      writeFileSync(fullPath, patched);
    }
  }
}

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
    if (mode === 'node') {
      patchNodeAddonLoaders(destination);
    }
    console.log(`${mode}: ${source} -> ${destination}`);
  }
}
