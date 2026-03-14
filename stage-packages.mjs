#!/usr/bin/env node
import { cpSync, existsSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const browserFrameworkSource = join(__dirname, 'Motely.BrowserWasm', 'bin', 'Release', 'net10.0-browser', 'publish', 'wwwroot', '_framework');
const singleThreadFrameworkSource = join(__dirname, 'Motely.SingleThread', 'bin', 'Release', 'net10.0-browser', 'publish', 'wwwroot', '_framework');
const targets = {
  browser: [
    { source: browserFrameworkSource, destination: join(__dirname, 'Motely.npm', '_framework') },
  ],
  singlethread: [
    { source: singleThreadFrameworkSource, destination: join(__dirname, 'Motely.npm', '_framework_st') },
  ],
};

const modes = process.argv.slice(2);
const selectedModes = modes.length === 0 || modes.includes('all')
  ? ['browser', 'singlethread']
  : modes;

const shouldInclude = (path) => {
  const normalized = path.replace(/\\/g, '/');
  if (normalized.endsWith('.br') || normalized.endsWith('.gz')) return false;
  if (normalized.includes('/debug/') || normalized.includes('/tmp/')) return false;
  return true;
};

// worker.js host template — written into _framework/ and _framework_st/ after staging.
// Boots the .NET WASM runtime inside a Web Worker and dispatches messages to [JSExport] methods.
// See: https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-on-webworkers
const WORKER_JS = `\
// Motely WASM Web Worker host
// See: https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-on-webworkers
import { dotnet } from './dotnet.js';

let raw = null;
let startupError = undefined;

try {
  const { getAssemblyExports, getConfig } = await dotnet.create();
  const config = getConfig();
  const allExports = await getAssemblyExports(config.mainAssemblyName);
  raw = allExports.Motely.BrowserWasm.MotelyWasmExports;
  dotnet.run().catch(() => {});
} catch (err) {
  startupError = err?.message ?? String(err);
}

self.addEventListener('message', async function (e) {
  const { command, requestId } = e.data;

  if (!raw) {
    self.postMessage({ command: 'response', requestId, error: startupError || 'WASM not loaded' });
    return;
  }

  try {
    let result = null;
    switch (command) {
      case 'getVersion':
        result = await raw.GetVersionAsync();
        break;
      case 'getCapabilities':
        result = await raw.GetCapabilitiesAsync();
        break;
      case 'analyzeSeed':
        result = await raw.AnalyzeSeedAsync(e.data.seed, e.data.deck, e.data.stake);
        break;
      case 'validateJaml':
        result = await raw.ValidateJamlAsync(e.data.jamlContent);
        break;
      case 'startJamlSearch':
        result = await raw.StartJamlSearch(
          e.data.jamlContent,
          e.data.optionsJson,
          (seedsSearched, matchingSeeds, elapsedMs) => {
            self.postMessage({ command: 'progress', requestId, seedsSearched, matchingSeeds, elapsedMs });
          },
          (seed, score) => {
            self.postMessage({ command: 'result', requestId, seed, score });
          }
        );
        break;
      case 'stopSearch':
        raw.StopSearch();
        result = 'ok';
        break;
      case 'disposeSearch':
        await raw.DisposeSearch();
        result = 'ok';
        break;
      default:
        throw new Error('Unknown command: ' + command);
    }
    self.postMessage({ command: 'response', requestId, result });
  } catch (err) {
    self.postMessage({ command: 'response', requestId, error: err?.message ?? String(err) });
  }
});
`;

for (const mode of selectedModes) {
  if (!(mode in targets)) {
    console.error(`Unknown staging target: ${mode}`);
    process.exit(1);
  }

  for (const { source, destination } of targets[mode]) {
    if (!existsSync(source)) {
      console.error(`Missing publish output for ${mode}: ${source}`);
      process.exit(1);
    }

    rmSync(destination, { recursive: true, force: true });
    cpSync(source, destination, {
      recursive: true,
      force: true,
      filter: (src) => shouldInclude(src),
    });
    console.log(`${mode}: ${source} -> ${destination}`);

    // Write the worker.js host into browser framework folders
    if (mode === 'browser' || mode === 'singlethread') {
      const workerDest = join(destination, 'worker.js');
      writeFileSync(workerDest, WORKER_JS, 'utf8');
      console.log(`${mode}: wrote worker.js -> ${workerDest}`);
    }
  }
}
