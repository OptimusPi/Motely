import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '../../');

const buildOutput = path.join(repoRoot, 'Motely.BrowserWasm/bin/Release/net10.0-browser/browser-wasm/publish');
const motelyWasmDist = path.join(repoRoot, 'motely-wasm/dist');
const stagingDir = path.join(repoRoot, 'Motely.npm-staging/motely-wasm');
const sourcePackageJson = path.join(repoRoot, 'motely-wasm/package.json');

// Ensure directories exist
fs.mkdirSync(motelyWasmDist, { recursive: true });
fs.mkdirSync(stagingDir, { recursive: true });

// Copy all files from build output to dist
if (fs.existsSync(buildOutput)) {
  fs.cpSync(buildOutput, motelyWasmDist, { recursive: true, force: true });
  console.log(`✓ Copied WASM build output to ${motelyWasmDist}`);
} else {
  throw new Error(`Build output not found at ${buildOutput}`);
}

// Copy package.json to staging
if (fs.existsSync(sourcePackageJson)) {
  fs.copyFileSync(sourcePackageJson, path.join(stagingDir, 'package.json'));
  console.log(`✓ Copied package.json to staging`);
} else {
  throw new Error(`package.json not found at ${sourcePackageJson}`);
}

// Copy dist to staging
fs.cpSync(motelyWasmDist, path.join(stagingDir, 'dist'), { recursive: true, force: true });
console.log(`✓ Staged motely-wasm to ${stagingDir}`);
