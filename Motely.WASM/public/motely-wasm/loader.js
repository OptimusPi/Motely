import { initDuckDbWasmResults as initDuckDbWasmResultsInternal, } from './duckdb';
/**
 * Loads the Motely WASM runtime from the specified base URL.
 * Works with Next.js and other bundlers by using a script tag injection approach.
 *
 * @param baseUrl The public path where the motely-wasm files are served (e.g., '/motely-wasm' or 'https://cdn.example.com/motely').
 * @returns A promise that resolves to the MotelyWasmApi.
 */
export async function loadMotely(baseUrl = '/motely-wasm') {
    // Ensure trailing slash is removed for consistency
    const cleanBase = baseUrl.replace(/\/$/, '');
    const dotnetUrl = `${cleanBase}/_framework/dotnet.js`;
    const bootConfigUrl = `${cleanBase}/_framework/dotnet.boot.js`;
    console.log(`[motely-wasm] Loading runtime from: ${dotnetUrl}`);
    try {
        // Load dotnet.js as a script tag to avoid bundler static analysis issues
        const dotnet = await loadScriptGlobal(dotnetUrl);
        const { getAssemblyExports, getConfig } = await dotnet
            .withConfigSrc(bootConfigUrl)
            .create();
        const config = getConfig();
        const exports = await getAssemblyExports(config.mainAssemblyName);
        const api = exports.Motely.WASM.MotelyWasm;
        return api;
    }
    catch (err) {
        console.error(`[motely-wasm] Failed to load WASM runtime:`, err);
        throw new Error(`Motely WASM Init Failed: ${err.message || String(err)}`);
    }
}
/**
 * Loads a script via a <script> tag and waits for it to define a global.
 * Returns the global reference or throws if not found.
 */
function loadScriptGlobal(url, globalName = 'dotnet') {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = url;
        script.type = 'text/javascript';
        script.async = true;
        script.onload = () => {
            const global = globalThis[globalName];
            if (global) {
                resolve(global);
            }
            else {
                reject(new Error(`Global '${globalName}' not found after loading ${url}`));
            }
        };
        script.onerror = () => {
            reject(new Error(`Failed to load script: ${url}`));
        };
        document.head.appendChild(script);
    });
}
/**
 * Initialize DuckDB-WASM results storage and hook into MotelyWasmOnResult.
 * Call this before starting a search if you want browser-side results persisted in DuckDB-WASM.
 */
export async function initDuckDbWasmResults(options = {}) {
    return initDuckDbWasmResultsInternal(options);
}
