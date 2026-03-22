#!/usr/bin/env node
import { copyFileSync, cpSync, existsSync, mkdirSync, writeFileSync } from 'fs';
import { dirname, join, resolve } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..', '..');
const pkgRoot = join(repoRoot, 'motely-wasm');
const distRoot = join(pkgRoot, 'dist');

const wasmPublish =
  process.env.MOTELY_WASM_PUBLISH_DIR ||
  join(repoRoot, 'Motely', 'bin', 'Release', 'net10.0-browser', 'publish');
const bootsharpSrc = join(wasmPublish, 'bootsharp');

if (!existsSync(bootsharpSrc)) {
  console.error(`Bootsharp folder not found at ${bootsharpSrc}`);
  console.error('Publish net10.0-browser first (e.g. dotnet publish Motely -f net10.0-browser -c Release).');
  process.exit(1);
}

mkdirSync(join(distRoot, 'bootsharp'), { recursive: true });
cpSync(bootsharpSrc, join(distRoot, 'bootsharp'), { recursive: true });
console.log('→ motely-wasm/dist/bootsharp/');

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
writeFileSync(join(distRoot, 'index.mjs'), wasmEntry);
console.log('→ motely-wasm/dist/index.mjs');

const stubDts = `export function boot(): Promise<void>;
export class MotelyWasm {}
export class Event {}
`;
writeFileSync(join(distRoot, 'index.d.ts'), stubDts);

const schemaJson = join(repoRoot, 'Motely', 'jaml.schema.json');
if (existsSync(schemaJson)) {
  copyFileSync(schemaJson, join(distRoot, 'jaml.schema.json'));
  console.log('→ motely-wasm/dist/jaml.schema.json');
}
