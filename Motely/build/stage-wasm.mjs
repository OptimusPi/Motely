import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(__dirname, '../../');
const motelyWasmCompatSnippet = `

const MotelyWasm = {
  MotelyBrowserApi: {
    createSingleSearchContext(seed, deck, stake) {
      const sessionId = getExports().Motely_Executors_MotelyWasm_CreateSingleSearchContext(seed, deck, stake);
      let disposed = false;
      return {
        beginShopStream(ante) {
          if (disposed) throw new Error('motely-wasm: single-search context already disposed');
          getExports().Motely_Executors_MotelyWasm_BeginShopStream(sessionId, ante);
        },
        getNextShopItem() {
          if (disposed) throw new Error('motely-wasm: single-search context already disposed');
          return JSON.parse(getExports().Motely_Executors_MotelyWasm_GetNextShopItemJson(sessionId));
        },
        dispose() {
          if (disposed) return;
          disposed = true;
          getExports().Motely_Executors_MotelyWasm_DisposeSingleSearchContext(sessionId);
        }
      };
    },
    getVersion() {
      return getExports().Motely_Executors_MotelyWasm_GetVersion();
    }
  },
  MotelyWasmBackend: {
    createInstance() {
      return getExports().Motely_Executors_MotelyWasm_CreateInstance();
    },
    destroyInstance(instanceId) {
      getExports().Motely_Executors_MotelyWasm_DestroyInstance(instanceId);
    },
    analyzeSeed(instanceId, seed, deck, stake) {
      return Promise.resolve(getExports().Motely_Executors_MotelyWasm_AnalyzeSeed(instanceId, seed, deck, stake));
    }
  }
};

export { MotelyWasm };
`;
const motelyWasmCompatTypesSnippet = `

export interface MotelyWasmSingleSearchContext {
  beginShopStream(ante: number): void;
  getNextShopItem(): { id: string; name: string; value: number };
  dispose(): void;
}

export declare const MotelyWasm: {
  MotelyBrowserApi: {
    createSingleSearchContext(seed: string, deck: string, stake: string): MotelyWasmSingleSearchContext;
    getVersion(): string;
  };
  MotelyWasmBackend: {
    createInstance(): number;
    destroyInstance(instanceId: number): void;
    analyzeSeed(instanceId: number, seed: string, deck: string, stake: string): Promise<string>;
  };
};
`;

const publishOut = path.join(repoRoot, 'Motely.Orchestration/bin/Release/net10.0/browser-wasm/publish');
const bootsharpOut = fs.existsSync(path.join(publishOut, 'index.mjs'))
  ? publishOut
  : path.join(repoRoot, 'Motely.Orchestration/bin/bootsharp');
const wasmDist = path.join(repoRoot, 'motely-wasm/dist');
const schema = path.join(repoRoot, 'jaml.schema.json');

if (!fs.existsSync(path.join(bootsharpOut, 'index.mjs'))) {
  throw new Error(`Bootsharp output not found at ${bootsharpOut}\nRun: dotnet publish Motely.Orchestration -c Release -p:WasmBuild=true`);
}

fs.mkdirSync(wasmDist, { recursive: true });

// index.mjs — the entire bundle (runtime + wasm + interop, one file)
const stagedIndexPath = path.join(wasmDist, 'index.mjs');
fs.copyFileSync(path.join(bootsharpOut, 'index.mjs'), stagedIndexPath);
const stagedIndex = fs.readFileSync(stagedIndexPath, 'utf8');
if (!stagedIndex.includes('export { MotelyWasm }')) {
  fs.writeFileSync(stagedIndexPath, stagedIndex + motelyWasmCompatSnippet, 'utf8');
}
console.log('staged index.mjs');

// TypeScript declarations
if (fs.existsSync(path.join(bootsharpOut, 'types'))) {
  fs.cpSync(path.join(bootsharpOut, 'types'), path.join(wasmDist, 'types'), { recursive: true, force: true });
  const stagedTypesIndexPath = path.join(wasmDist, 'types', 'index.d.ts');
  if (fs.existsSync(stagedTypesIndexPath)) {
    const stagedTypesIndex = fs.readFileSync(stagedTypesIndexPath, 'utf8');
    if (!stagedTypesIndex.includes('export declare const MotelyWasm')) {
      fs.writeFileSync(stagedTypesIndexPath, stagedTypesIndex + motelyWasmCompatTypesSnippet, 'utf8');
    }
  }
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
