#!/usr/bin/env node
import { copyFileSync, existsSync, mkdirSync } from 'fs';
import { dirname, join, resolve } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..', '..');
const pkgRoot = join(repoRoot, 'motely-node');
const binDir = join(pkgRoot, 'bin', 'linux-x64');

const publishDir =
  process.env.MOTELY_NODE_PUBLISH_DIR ||
  join(repoRoot, 'Motely', 'bin', 'Release', 'net10.0', 'publish');
const addonSrc = join(publishDir, 'motely.node');

mkdirSync(binDir, { recursive: true });

if (!existsSync(addonSrc)) {
  console.error(`motely.node not found at ${addonSrc}`);
  console.error('Build linux-x64 first: ./scripts/build-node-linux.sh (WSL) or see scripts/build-node-docker.sh');
  process.exit(1);
}

copyFileSync(addonSrc, join(binDir, 'motely.node'));
console.log(`→ motely-node/bin/linux-x64/motely.node`);

const schemaJson = join(repoRoot, 'Motely', 'jaml.schema.json');
if (existsSync(schemaJson)) {
  copyFileSync(schemaJson, join(pkgRoot, 'jaml.schema.json'));
  console.log('→ motely-node/jaml.schema.json');
}
