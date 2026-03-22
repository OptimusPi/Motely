#!/usr/bin/env node
import { mkdirSync, copyFileSync, writeFileSync, existsSync } from 'fs';
import { join, resolve, dirname } from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const root = resolve(__dirname, '..', '..');
const distRoot = join(root, 'motely', 'dist');

console.log('🔨 Staging motely artifacts\n');

mkdirSync(join(distRoot, 'node'), { recursive: true });
mkdirSync(join(distRoot, 'wasm'), { recursive: true });

const nodePublish = join(root, 'Motely', 'bin', 'Release', 'net10.0', 'publish');
const wasmPublish = join(root, 'Motely', 'bin', 'Release', 'net10.0-browser', 'publish');

// Node: Copy NodeApi-generated bindings + managed DLL
console.log('📦 Node.js bindings');
['Motely.js', 'Motely.d.ts', 'Motely.dll', 'Motely.deps.json', 'import.cjs'].forEach(file => {
  const src = join(nodePublish, file);
  if (existsSync(src)) {
    copyFileSync(src, join(distRoot, 'node', file));
    console.log(`  ✓ ${file}`);
  }
});

// Node: Copy transitive dependencies (YamlDotNet, ImageSharp, etc.)
['YamlDotNet.dll', 'SixLabors.ImageSharp.dll', 'JetBrains.Annotations.dll', 'Microsoft.JavaScript.NodeApi.dll'].forEach(file => {
  const src = join(nodePublish, file);
  if (existsSync(src)) {
    copyFileSync(src, join(distRoot, 'node', file));
  }
});

// Node: Create entry point that uses NodeApi loader
const nodeEntry = `export async function boot() {
  // NodeApi runtime is loaded via Motely.js
}

// Re-export everything from NodeApi-generated bindings
export * from './Motely.js';
`;
writeFileSync(join(distRoot, 'node', 'index.mjs'), nodeEntry);
console.log('  ✓ index.mjs (export wrapper)');

// WASM: Copy Bootsharp runtime
console.log('\n📦 WASM + Bootsharp');
const bootsharpSrc = join(wasmPublish, 'bootsharp');
if (existsSync(bootsharpSrc)) {
  mkdirSync(join(distRoot, 'wasm', 'bootsharp'), { recursive: true });
  // Copy all Bootsharp files
  const bootsharpFiles = ['index.mjs', 'index.wasm', 'index.d.ts'].filter(f =>
    existsSync(join(bootsharpSrc, f))
  );
  bootsharpFiles.forEach(file => {
    copyFileSync(join(bootsharpSrc, file), join(distRoot, 'wasm', 'bootsharp', file));
  });
  console.log(`  ✓ bootsharp/ (${bootsharpFiles.length} files)`);
}

// WASM: Create entry point
const wasmEntry = `import bootsharp, { MotelyWasm, Event } from "./bootsharp/index.mjs";

let bootPromise = null;

export function boot() {
    if (!bootPromise) {
        const root = new URL("./bootsharp", import.meta.url).href;
        bootPromise = bootsharp.boot({ root });
    }
    return bootPromise;
}

export { MotelyWasm, Event };
`;
writeFileSync(join(distRoot, 'wasm', 'index.mjs'), wasmEntry);
console.log('  ✓ index.mjs (Bootsharp wrapper)');

// Root types
const defs = `export { MotelyWasm, Event } from './wasm/index';
export function boot(): Promise<void>;
`;
writeFileSync(join(distRoot, 'index.d.ts'), defs);

// Schema
const schemaJson = join(root, 'Motely', 'jaml.schema.json');
if (existsSync(schemaJson)) {
  copyFileSync(schemaJson, join(distRoot, 'jaml.schema.json'));
  console.log('\n📋 jaml.schema.json');
}

console.log('\n✅ Ready');
