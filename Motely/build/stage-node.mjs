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
// NativeAOT produces Motely.so on linux — rename to .node for Node.js
const addonSrc = existsSync(join(publishDir, 'Motely.so'))
  ? join(publishDir, 'Motely.so')
  : join(publishDir, 'motely.node');

mkdirSync(binDir, { recursive: true });

if (!existsSync(addonSrc)) {
  console.error(`Native addon not found at ${publishDir} (looked for Motely.so and motely.node)`);
  console.error('Build linux-x64 NativeAOT first: docker run ... dotnet publish -r linux-x64 -p:PublishAot=true');
  process.exit(1);
}

copyFileSync(addonSrc, join(binDir, 'motely.node'));
console.log(`→ motely-node/bin/linux-x64/motely.node`);

const schemaJson = join(repoRoot, 'Motely', 'jaml.schema.json');
if (existsSync(schemaJson)) {
  copyFileSync(schemaJson, join(pkgRoot, 'jaml.schema.json'));
  console.log('→ motely-node/jaml.schema.json');
}
