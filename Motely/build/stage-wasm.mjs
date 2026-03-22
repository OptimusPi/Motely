#!/usr/bin/env node
/**
 * Stage Bootsharp browser output into `motely-wasm/dist/` for npm.
 *
 * `dotnet publish Motely.BrowserWasm` writes a **flat** ES module bundle under
 * `Motely.BrowserWasm/bin/<cfg>/net10.0-browser/browser-wasm/publish/` (not under `Motely/bin/...`).
 * Bootsharp may also mirror dotnet assets into `motely-wasm/dist/bootsharp` during publish; this script
 * replaces `dist/bootsharp` with the **full** publish tree so `./bootsharp/index.mjs` and relative
 * imports (`./dotnet.js`, etc.) resolve. Then writes `dist/index.mjs` (thin `boot()` wrapper).
 */
import { copyFileSync, cpSync, existsSync, mkdirSync, readdirSync, statSync, writeFileSync } from 'fs';
import { dirname, join, resolve } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..', '..');
const distRoot = join(repoRoot, 'motely-wasm', 'dist');
const bootsharpDest = join(distRoot, 'bootsharp');
const typesDest = join(distRoot, 'types');

const wasmPublish =
    process.env.MOTELY_WASM_PUBLISH_DIR ||
    join(
        repoRoot,
        'Motely.BrowserWasm',
        'bin',
        'Release',
        'net10.0-browser',
        'browser-wasm',
        'publish',
    );

const indexMjs = join(wasmPublish, 'index.mjs');
if (!existsSync(indexMjs)) {
    console.error(`WASM publish output not found (expected index.mjs):\n  ${wasmPublish}`);
    console.error('Publish first: dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release');
    process.exit(1);
}

mkdirSync(bootsharpDest, { recursive: true });
cpSync(wasmPublish, bootsharpDest, { recursive: true });
console.log(`→ motely-wasm/dist/bootsharp/ (${wasmPublish})`);

const wasmEntry = `import bootsharp, { MotelyWasm, Event } from "./bootsharp/index.mjs";

let bootPromise = null;

export function boot() {
  if (!bootPromise) {
    const root = new URL("./bootsharp/", import.meta.url).href;
    bootPromise = bootsharp.boot({ root });
  }
  return bootPromise;
}

export { MotelyWasm, Event };

const defaultExport = { boot, MotelyWasm, Event };
export default defaultExport;
`;
writeFileSync(join(distRoot, 'index.mjs'), wasmEntry);
console.log('→ motely-wasm/dist/index.mjs');

mkdirSync(typesDest, { recursive: true });
const dtsFiles = readdirSync(wasmPublish).filter((f) => f.endsWith('.d.ts'));
for (const f of dtsFiles) {
    const src = join(wasmPublish, f);
    if (statSync(src).isFile()) {
        copyFileSync(src, join(typesDest, f));
    }
}
if (dtsFiles.length) {
    console.log(`→ motely-wasm/dist/types/*.d.ts (${dtsFiles.length} files)`);
}

const schemaCandidates = [join(repoRoot, 'jaml.schema.json'), join(repoRoot, 'Motely', 'jaml.schema.json')];
for (const p of schemaCandidates) {
    if (existsSync(p)) {
        copyFileSync(p, join(distRoot, 'jaml.schema.json'));
        console.log('→ motely-wasm/dist/jaml.schema.json');
        break;
    }
}
