#!/usr/bin/env node
/**
 * Prepares the npm package by copying the WASM publish output to dist/
 * Run from Motely.WASM: npm run build (runs dotnet publish then this script)
 */
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, '..');
const base = path.join(root, 'bin', 'Release', 'net10.0-browser', 'browser-wasm');
const distDir = path.join(root, 'dist');

const candidates = [
    path.join(base, 'AppBundle'),
    path.join(base, 'publish'),
];

let srcDir = null;
for (const dir of candidates) {
    if (fs.existsSync(dir) && fs.existsSync(path.join(dir, '_framework'))) {
        srcDir = dir;
        break;
    }
}

if (!srcDir) {
    console.error('ERROR: No WASM publish output found.');
    console.error('  Looked in: ' + base);
    console.error('  Tried: AppBundle, publish (each with _framework)');
    console.error('  Run from Motely.WASM: dotnet publish -c Release');
    process.exit(1);
}

if (fs.existsSync(distDir)) {
    fs.rmSync(distDir, { recursive: true });
}

fs.cpSync(srcDir, distDir, { recursive: true });
const frameworkCount = fs.readdirSync(path.join(distDir, '_framework')).length;
console.log('Copied to dist/ (' + frameworkCount + ' files in _framework)');
