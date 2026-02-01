#!/usr/bin/env node
/**
 * Prepares the npm package by copying the WASM AppBundle to dist/
 * Run after: dotnet publish -c Release
 */
const fs = require('fs');
const path = require('path');

const srcDir = path.join(__dirname, '..', 'bin', 'Release', 'net10.0-browser', 'browser-wasm', 'AppBundle');
const distDir = path.join(__dirname, '..', 'dist');

if (!fs.existsSync(srcDir)) {
    console.error('ERROR: AppBundle not found. Run "dotnet publish -c Release" first.');
    process.exit(1);
}

// Clean dist
if (fs.existsSync(distDir)) {
    fs.rmSync(distDir, { recursive: true });
}

// Copy AppBundle to dist
fs.cpSync(srcDir, distDir, { recursive: true });

console.log(`✅ Copied AppBundle to dist/`);
console.log(`   Framework files: ${fs.readdirSync(path.join(distDir, '_framework')).length}`);
