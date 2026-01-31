#!/usr/bin/env node
/**
 * Copies the .NET WASM AppBundle (or publish output) into dist/app-bundle
 * so the NPM package can ship the pre-built assets. Consumers copy this to public/.
 */

const fs = require('fs');
const path = require('path');

const projectRoot = path.resolve(__dirname, '..');
const binRoot = path.join(projectRoot, 'bin', 'Release', 'net10.0-browser');
const outDir = path.join(projectRoot, 'dist', 'app-bundle');

// Use AppBundle (has _framework/ structure with .wasm files and dotnet.boot.js)
// Do NOT use 'publish' - it has flat structure without dotnet.boot.js
const candidates = [
  path.join(binRoot, 'browser-wasm', 'AppBundle'),
];

let sourceDir = null;
for (const dir of candidates) {
  if (fs.existsSync(dir)) {
    sourceDir = dir;
    break;
  }
}

if (!sourceDir) {
  console.error('No AppBundle or publish folder found. Run: dotnet publish -c Release');
  process.exit(1);
}

function copyRecursive(src, dest) {
  const stat = fs.statSync(src);
  if (stat.isDirectory()) {
    if (!fs.existsSync(dest)) fs.mkdirSync(dest, { recursive: true });
    for (const name of fs.readdirSync(src)) {
      copyRecursive(path.join(src, name), path.join(dest, name));
    }
  } else {
    fs.mkdirSync(path.dirname(dest), { recursive: true });
    fs.copyFileSync(src, dest);
  }
}

if (fs.existsSync(outDir)) {
  fs.rmSync(outDir, { recursive: true });
}
fs.mkdirSync(path.dirname(outDir), { recursive: true });
copyRecursive(sourceDir, outDir);
console.log('Copied', sourceDir, '->', outDir);
