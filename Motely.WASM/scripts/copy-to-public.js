#!/usr/bin/env node
/**
 * Copies dist/app-bundle to <cwd>/public/motely-wasm (or PUBLIC_DIR env).
 * Run from your app root: npx motely-wasm-copy-to-public
 */

const fs = require('fs');
const path = require('path');

const bundleDir = path.join(__dirname, '..', 'dist', 'app-bundle');
const publicDir = process.env.PUBLIC_DIR || path.join(process.cwd(), 'public');
const targetDir = path.join(publicDir, 'motely-wasm');

if (!fs.existsSync(bundleDir)) {
  console.error('motely-wasm: dist/app-bundle not found. Run npm run build in the motely-wasm package first.');
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

if (fs.existsSync(targetDir)) fs.rmSync(targetDir, { recursive: true });
copyRecursive(bundleDir, targetDir);
// Single entry point: host app serves its own index at /. Overwrite bundle index with redirect.
const redirectHtml = '<!DOCTYPE html><html><head><meta charset="UTF-8"><meta http-equiv="refresh" content="0;url=/"><title>Redirect</title></head><body>Redirecting to <a href="/">app</a>...</body></html>';
fs.writeFileSync(path.join(targetDir, 'index.html'), redirectHtml);
console.log('motely-wasm: copied to', targetDir);
