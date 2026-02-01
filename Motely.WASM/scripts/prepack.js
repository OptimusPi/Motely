#!/usr/bin/env node
/**
 * Runs before npm pack/publish. Ensures dist/_framework exists so the tarball includes WASM files.
 * Resolves paths from this script's location (Motely.WASM/scripts/) so it works regardless of cwd.
 */
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, '..');
const frameworkDir = path.join(root, 'dist', '_framework');

if (!fs.existsSync(frameworkDir)) {
  console.error('ERROR: dist/_framework not found.');
  console.error('  Run from Motely.WASM directory: npm run build');
  console.error('  Then run: npm publish');
  process.exit(1);
}

console.log('prepack OK: dist/_framework present');
