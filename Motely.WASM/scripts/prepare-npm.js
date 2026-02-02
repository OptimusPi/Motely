#!/usr/bin/env node
/**
 * Prepares the npm package by copying the WASM publish output to dist/
 * Run from Motely.WASM: npm run build (runs dotnet publish then this script)
 */
const fs = require('fs');
const path = require('path');

const root = path.join(__dirname, '..');
const distDir = path.join(root, 'dist');

// Try multiple possible .NET WASM publish output locations
const candidates = [
    // Net 8/9 style: bin/Release/net8.0-browser/browser-wasm/publish
    path.join(root, 'bin', 'Release', 'net8.0-browser', 'browser-wasm', 'publish'),
    path.join(root, 'bin', 'Release', 'net8.0-browser', 'browser-wasm', 'AppBundle'),
    // Net 10 style: bin/Release/net10.0-browser/browser-wasm/publish
    path.join(root, 'bin', 'Release', 'net10.0-browser', 'browser-wasm', 'publish'),
    path.join(root, 'bin', 'Release', 'net10.0-browser', 'browser-wasm', 'AppBundle'),
    // Alternative: bin/Release/net10.0-browser/publish
    path.join(root, 'bin', 'Release', 'net10.0-browser', 'publish'),
];

let srcDir = null;
for (const dir of candidates) {
    if (fs.existsSync(dir) && fs.existsSync(path.join(dir, '_framework'))) {
        srcDir = dir;
        console.log('Found WASM output at: ' + dir);
        break;
    }
}

if (!srcDir) {
    console.error('ERROR: No WASM publish output found.');
    console.error('  Tried:');
    candidates.forEach(dir => console.error('    - ' + dir));
    console.error('  Run from Motely.WASM: dotnet publish -c Release');
    process.exit(1);
}

if (fs.existsSync(distDir)) {
    fs.rmSync(distDir, { recursive: true });
}

fs.cpSync(srcDir, distDir, { recursive: true });
const frameworkCount = fs.readdirSync(path.join(distDir, '_framework')).length;
console.log('Copied to dist/ (' + frameworkCount + ' files in _framework)');
