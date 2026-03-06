// Motely Node.js Package Entry Point
// Prefer node-api-dotnet addon when addonPath is set; otherwise use browser WASM build with Node.js runtime.
// Per https://microsoft.github.io/node-api-dotnet/reference/js/ and scenarios/js-dotnet-module.html
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { pathToFileURL } from 'node:url';
function _dir() {
    if (typeof import.meta !== 'undefined' && import.meta.url) {
        return dirname(fileURLToPath(import.meta.url));
    }
    return typeof __dirname !== 'undefined' ? __dirname : '.';
}
/**
 * Build the unified API from raw .NET exports (WASM or addon).
 */
function buildApi(raw, cachedCapabilities) {
    return {
        async getCapabilities() {
            return cachedCapabilities;
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
            const withDefaults = {
                threadCount: 1,
                batchCharCount: 4,
                ...searchParams,
            };
            const optionsJson = JSON.stringify(withDefaults);
            const isAddon = typeof raw.GetSearchStatus === 'function';
            if (isAddon && (onProgress || onResult)) {
                // Addon: poll GetSearchStatus for progress/results (no JS globals).
                const startPromise = raw.StartJamlSearch(jamlContent, optionsJson);
                const interval = setInterval(async () => {
                    try {
                        const statusJson = await raw.GetSearchStatus();
                        const status = JSON.parse(statusJson);
                        if (status.error)
                            return;
                        if (onProgress)
                            onProgress(status.totalSeedsSearched ?? 0, status.matchingSeeds ?? 0, status.elapsedMs ?? 0, status.results?.length ?? 0);
                        if (onResult && status.results)
                            for (const r of status.results)
                                onResult(r.seed, r.score);
                        if (!status.isRunning)
                            clearInterval(interval);
                    }
                    catch {
                        /* ignore */
                    }
                }, 200);
                try {
                    await startPromise;
                }
                finally {
                    clearInterval(interval);
                }
            }
            else if (isAddon) {
                const resultJson = await raw.StartJamlSearch(jamlContent, optionsJson);
                const result = JSON.parse(resultJson);
                if (result.error)
                    throw new Error(result.error);
            }
            else {
                // WASM only: .NET calls back via global __motelyOnProgress / __motelyOnResult ([JSImport]).
                globalThis.__motelyOnProgress = onProgress ?? (() => { });
                globalThis.__motelyOnResult = onResult ?? (() => { });
                try {
                    const resultJson = await raw.StartJamlSearch(jamlContent, optionsJson);
                    const result = JSON.parse(resultJson);
                    if (result.error)
                        throw new Error(result.error);
                }
                finally {
                    globalThis.__motelyOnProgress = () => { };
                    globalThis.__motelyOnResult = () => { };
                }
            }
        },
        dispose: () => {
            void raw.DisposeSearch();
        },
    };
}
/**
 * Load Motely for Node.js. Prefer node-api-dotnet addon when addonPath is set; otherwise use WASM.
 * Call once at app startup; the returned object is reusable.
 * @see https://microsoft.github.io/node-api-dotnet/reference/js/
 */
export async function loadMotely(options) {
    if (options?.addonPath) {
        const dotnet = await import('node-api-dotnet');
        const m = dotnet.require(options.addonPath);
        const raw = m.MotelyNodeExports ?? m;
        const [versionJson, capabilitiesJson] = await Promise.all([
            raw.GetVersionAsync(),
            raw.GetCapabilitiesAsync(),
        ]);
        const cachedCapabilities = JSON.parse(capabilitiesJson);
        return buildApi(raw, cachedCapabilities);
    }
    // WASM path
    globalThis.__motelyOnProgress = () => { };
    globalThis.__motelyOnResult = () => { };
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
    const [versionJson, capabilitiesJson] = await Promise.all([
        raw.GetVersionAsync(),
        raw.GetCapabilitiesAsync(),
    ]);
    const cachedCapabilities = JSON.parse(capabilitiesJson);
    return buildApi(raw, cachedCapabilities);
}
