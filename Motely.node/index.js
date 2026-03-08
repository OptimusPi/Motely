// Motely Node.js Package — WASM-based Balatro seed engine
// Wraps .NET WASM exports (Motely.SingleThread / Motely.BrowserWasm)
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { pathToFileURL } from 'node:url';
function _dir() {
    return dirname(fileURLToPath(import.meta.url));
}
function buildApi(raw, cachedCapabilities) {
    return {
        async getCapabilities() {
            return cachedCapabilities;
        },
        getAvailableThreadCount() {
            return cachedCapabilities.availableThreadCount;
        },
        async analyzeSeed(seed, deck, stake) {
            const json = await raw.AnalyzeSeedAsync(seed, deck, stake);
            const result = JSON.parse(json);
            if (result.error && !result.seed)
                throw new Error(result.error);
            return result;
        },
        async validateJaml(jaml) {
            const json = await raw.ValidateJamlAsync(jaml);
            return JSON.parse(json);
        },
        async startJamlSearch(jamlContent, options) {
            const { onProgress, onResult, ...searchParams } = options ?? {};
            const optionsJson = JSON.stringify({
                threadCount: 1,
                batchCharCount: 4,
                ...searchParams,
            });
            const results = [];
            const progressCb = onProgress
                ? (json) => {
                    const p = JSON.parse(json);
                    onProgress(p.seedsSearched, p.matchingSeeds, p.elapsedMs, p.resultCount);
                }
                : () => { };
            const resultCb = (seed, score) => {
                results.push({ seed, score });
                onResult?.(seed, score);
            };
            const response = JSON.parse(await raw.StartJamlSearch(jamlContent, optionsJson, progressCb, resultCb));
            if (response.error)
                throw new Error(response.error);
            return results;
        },
        dispose() {
            void raw.DisposeSearch();
        },
    };
}
/**
 * Load the Motely WASM engine for Node.js. Call once at startup; reuse the returned API.
 */
export async function loadMotely(options) {
    const frameworkPath = options?.frameworkPath ?? join(_dir(), '_framework');
    const dotnetJsPath = join(frameworkPath, 'dotnet.js');
    const dotnetUrl = pathToFileURL(dotnetJsPath).href;
    const mod = await import(dotnetUrl);
    const dotnet = mod.dotnet;
    const runtime = await dotnet.withDiagnosticTracing(false).create();
    const config = runtime.getConfig();
    const allExports = (await runtime.getAssemblyExports(config.mainAssemblyName));
    const raw = allExports.Motely.BrowserWasm.MotelyWasmExports;
    runtime.runMain?.().catch((err) => console.error('[motely-node] runMain failed:', err));
    const capabilitiesJson = await raw.GetCapabilitiesAsync();
    const cachedCapabilities = JSON.parse(capabilitiesJson);
    return buildApi(raw, cachedCapabilities);
}
