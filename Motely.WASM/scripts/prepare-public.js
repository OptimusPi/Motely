#!/usr/bin/env node
/**
 * Copies motely-wasm dist from node_modules to your app's public dir.
 * Runs automatically on npm install (postinstall). Or run: npx motely-wasm-prepare
 *
 * Usage: motely-wasm-prepare [destination]
 * Default destination: public/motely-wasm (relative to project root)
 * Uses INIT_CWD when run as postinstall so we copy to the project that installed us.
 */
const fs = require('fs');
const path = require('path');

const projectRoot = process.env.INIT_CWD || process.cwd();
const destArg = process.argv[2];
const dest = path.resolve(projectRoot, destArg || path.join('public', 'motely-wasm'));

// Resolve package dist: this script lives in node_modules/motely-wasm/scripts/
const pkgRoot = path.join(__dirname, '..');
const src = path.join(pkgRoot, 'dist');

if (!fs.existsSync(src)) {
  console.error('motely-wasm: dist not found at', src);
  process.exit(1);
}

try {
  fs.mkdirSync(path.dirname(dest), { recursive: true });
  fs.cpSync(src, dest, { recursive: true });
  console.log('motely-wasm: copied to', dest);
} catch (err) {
  console.error('motely-wasm:', err.message);
  process.exit(1);
}
