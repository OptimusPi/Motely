import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '../../');

const bootsharpOut = path.join(repoRoot, 'Motely.Orchestration/bin/bootsharp');
const wasmDist = path.join(repoRoot, 'motely-wasm/dist');
const schema = path.join(repoRoot, 'jaml.schema.json');

if (!fs.existsSync(bootsharpOut)) {
  throw new Error(`Bootsharp output not found at ${bootsharpOut}\nRun: dotnet publish Motely.Orchestration -c Release -p:WasmBuild=true`);
}

fs.mkdirSync(wasmDist, { recursive: true });

// index.mjs — the entire bundle (runtime + wasm + interop, one file)
fs.copyFileSync(path.join(bootsharpOut, 'index.mjs'), path.join(wasmDist, 'index.mjs'));
console.log('staged index.mjs');

// TypeScript declarations
if (fs.existsSync(path.join(bootsharpOut, 'types'))) {
  fs.cpSync(path.join(bootsharpOut, 'types'), path.join(wasmDist, 'types'), { recursive: true, force: true });
  console.log('staged types/');
}

// JAML schema
if (fs.existsSync(schema)) {
  fs.copyFileSync(schema, path.join(wasmDist, 'jaml.schema.json'));
  console.log('staged jaml.schema.json');
}

// Sync version from Directory.Packages.props → motely-wasm/package.json
const props = fs.readFileSync(path.join(repoRoot, 'Directory.Packages.props'), 'utf8');
const m = props.match(/<MotelyVersion>([^<]+)<\/MotelyVersion>/);
if (m) {
  const pkgPath = path.join(repoRoot, 'motely-wasm/package.json');
  const pkg = JSON.parse(fs.readFileSync(pkgPath, 'utf8'));
  pkg.version = m[1].trim();
  fs.writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + '\n');
  console.log(`version → ${pkg.version}`);
}

console.log('done.');
