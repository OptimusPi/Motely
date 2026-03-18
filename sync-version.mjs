#!/usr/bin/env node
import { readFileSync, writeFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));

// Get version from argument (MinVer) or read from Directory.Packages.props
let motelyVersion = process.argv[2];

if (!motelyVersion) {
  const propsPath = join(__dirname, 'Directory.Packages.props');
  const propsContent = readFileSync(propsPath, 'utf8');
  const versionMatch = propsContent.match(/<MotelyVersion>(.*?)<\/MotelyVersion>/);

  if (!versionMatch) {
    console.error('Could not find <MotelyVersion> in Directory.Packages.props');
    process.exit(1);
  }

  motelyVersion = versionMatch[1];
}

console.log(`MotelyVersion: ${motelyVersion}`);

const packageJsonPaths = [
  join(__dirname, 'Motely.NodeAddon', 'package.json'),
  join(__dirname, 'motely-node', 'package.json'),
  join(__dirname, 'motely-wasm', 'package.json'),
];

for (const pkgPath of packageJsonPaths) {
  const pkg = JSON.parse(readFileSync(pkgPath, 'utf8'));
  const oldVersion = pkg.version;
  pkg.version = motelyVersion;
  writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + '\n', 'utf8');
  console.log(`${pkg.name}: ${oldVersion} -> ${motelyVersion}`);
}
