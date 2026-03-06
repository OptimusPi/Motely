// Node.js-compatible loader for Motely WASM
// Uses file:// URLs instead of HTTP for loading dotnet.js

import { fileURLToPath, pathToFileURL } from 'node:url';
import { dirname, join } from 'node:path';
import { existsSync } from 'node:fs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

/**
 * Load Motely WASM in Node.js
 * @param {Object} options
 * @param {string} options.frameworkPath - Path to _framework directory (default: auto-detect)
 */
export async function loadMotelyNode(options = {}) {
  const frameworkPath = options.frameworkPath || join(__dirname, '_framework');
  
  if (!existsSync(frameworkPath)) {
    throw new Error(`_framework not found at: ${frameworkPath}\n` +
      `Build the WASM project first: dotnet publish Motely.NodeWasm/Motely.BrowserWasm.csproj -c Release`);
  }

  const dotnetJsPath = join(frameworkPath, 'dotnet.js');
  
  if (!existsSync(dotnetJsPath)) {
    throw new Error(`dotnet.js not found at: ${dotnetJsPath}`);
  }

  // Convert to file:// URL for ES module import
  const dotnetUrl = pathToFileURL(dotnetJsPath).href;

  // Install callbacks before runtime boots
  globalThis.__motelyOnProgress = () => {};
  globalThis.__motelyOnResult = () => {};

  // Load the .NET WASM runtime
  const { dotnet } = await import(dotnetUrl);
  const runtime = await dotnet.create();
  const config = runtime.getConfig();
  
  // Get the WASM exports
  const allExports = await runtime.getAssemblyExports(config.mainAssemblyName);
  const raw = allExports.Motely?.BrowserWasm?.MotelyWasmExports;
  
  if (!raw) {
    throw new Error('Could not find MotelyWasmExports in assembly');
  }

  // Start the main program (it runs forever with Task.Delay(Timeout.Infinite))
  runtime.runMain().catch(err => {
    // Expected - keeps running
  });

  // Wait for initialization
  await new Promise(r => setTimeout(r, 100));

  // Cache version and capabilities
  const [versionJson, capabilitiesJson] = await Promise.all([
    raw.GetVersionAsync(),
    raw.GetCapabilitiesAsync(),
  ]);
  const cachedVersion = JSON.parse(versionJson);
  const cachedCapabilities = JSON.parse(capabilitiesJson);

  return {
    getVersion: () => cachedVersion,
    getCapabilities: () => cachedCapabilities,
    
    async analyzeSeed(seed, deck, stake) {
      const json = await raw.AnalyzeSeedAsync(seed, deck, stake);
      const result = JSON.parse(json);
      if (result.error && !result.seed) {
        throw new Error(result.error);
      }
      return result;
    },

    async validateJaml(jaml) {
      const json = await raw.ValidateJamlAsync(jaml);
      return JSON.parse(json);
    },

    async startJamlSearch(jamlContent, options = {}) {
      const { onProgress, onResult, ...searchParams } = options;
      
      globalThis.__motelyOnProgress = onProgress || (() => {});
      globalThis.__motelyOnResult = onResult || (() => {});
      
      const optionsJson = Object.keys(searchParams).length > 0 
        ? JSON.stringify(searchParams) 
        : '{}';
      
      const resultJson = await raw.StartJamlSearch(jamlContent, optionsJson);
      
      globalThis.__motelyOnProgress = () => {};
      globalThis.__motelyOnResult = () => {};
      
      const result = JSON.parse(resultJson);
      if (result.error) {
        throw new Error(result.error);
      }
      return result;
    },

    stopSearch: () => raw.StopSearch(),
    disposeSearch: () => raw.DisposeSearch(),
  };
}
