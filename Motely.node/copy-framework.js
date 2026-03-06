#!/usr/bin/env node
// Copy _framework from the SINGLE-THREADED build (Motely.SingleThread) to the Node.js package.
// The SingleThread build has WasmEnableThreads=false — no SharedArrayBuffer needed, works in Node.js.

import { cpSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

// Primary: published output from dotnet publish
const publishedFramework = join(__dirname, '..', 'Motely.SingleThread', 'bin', 'Release', 'net10.0-browser', 'publish', 'wwwroot', '_framework');
// Fallback: pre-copied MotelyNode/_framework (from the csproj CopyToNodePackage target)
const precopiedFramework = join(__dirname, '..', 'MotelyNode', '_framework');

const sourceFramework = existsSync(publishedFramework) ? publishedFramework : precopiedFramework;
const destFramework = join(__dirname, '_framework');

if (!existsSync(sourceFramework)) {
  console.error('Error: SingleThread WASM build not found.');
  console.error('Checked:', publishedFramework);
  console.error('Checked:', precopiedFramework);
  console.error('Build it first:');
  console.error('  dotnet publish ../Motely.SingleThread/Motely.SingleThread.csproj -c Release');
  process.exit(1);
}

console.log(`Copying _framework from: ${sourceFramework}`);
cpSync(sourceFramework, destFramework, { recursive: true, force: true });
console.log('Done — single-threaded WASM ready for Node.js');
