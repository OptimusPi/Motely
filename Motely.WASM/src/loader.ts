import type {
    MotelyWasmApi,
    SeedAnalysisResult,
    AnteAnalysis,
    ShopItem,
    Pack,
    SearchResponse,
    SearchHit,
    SearchProgress,
    ValidateResult,
    VersionInfo,
    ErrorResult,
} from '../motely-wasm';
import {
    initDuckDbWasmResults as initDuckDbWasmResultsInternal,
    type DuckDbWasmResultsHandle,
    type DuckDbWasmResultsOptions,
} from './duckdb';

/**
 * Loads the Motely WASM runtime from the specified base URL.
 * Use this in browser code when your bundler supports dynamic import of the WASM URL.
 *
 * @param baseUrl The public path where the motely-wasm files are served (e.g., '/motely-wasm' or 'https://cdn.example.com/motely').
 * @returns A promise that resolves to the MotelyWasmApi.
 */
export async function loadMotely(baseUrl: string = '/motely-wasm'): Promise<MotelyWasmApi> {
    // Ensure trailing slash is removed for consistency, then append _framework/dotnet.js
    const cleanBase = baseUrl.replace(/\/$/, '');
    const dotnetUrl = `${cleanBase}/_framework/dotnet.js`;
    const bootConfigUrl = `${cleanBase}/_framework/dotnet.boot.js`;

    console.log(`[motely-wasm] Loading runtime from: ${dotnetUrl}`);

    try {
        // dynamic import with webpackIgnore to prevent bundlers from trying to resolve this at build time.
        // @ts-ignore
        const { dotnet } = await import(/* webpackIgnore: true */ /* @vite-ignore */ dotnetUrl);

        const { getAssemblyExports, getConfig } = await dotnet
            .withConfigSrc(bootConfigUrl)
            .create();

        const config = getConfig();
        const exports = await getAssemblyExports(config.mainAssemblyName);
        const api = exports.Motely.WASM.MotelyWasm as MotelyWasmApi;

        return api;
    } catch (err: any) {
        console.error(`[motely-wasm] Failed to load WASM runtime:`, err);
        throw new Error(`Motely WASM Init Failed: ${err.message || String(err)}`);
    }
}

/**
 * Initialize DuckDB-WASM results storage and hook into MotelyWasmOnResult.
 * Call this before starting a search if you want browser-side results persisted in DuckDB-WASM.
 */
export async function initDuckDbWasmResults(
    options: DuckDbWasmResultsOptions = {}
): Promise<DuckDbWasmResultsHandle> {
    return initDuckDbWasmResultsInternal(options);
}

// Type-only re-exports so consumers get types without requiring a runtime motely-wasm.js
export type {
    MotelyWasmApi,
    SeedAnalysisResult,
    AnteAnalysis,
    ShopItem,
    Pack,
    SearchResponse,
    SearchHit,
    SearchProgress,
    ValidateResult,
    VersionInfo,
    ErrorResult,
};

export type { DuckDbWasmResultsHandle, DuckDbWasmResultsOptions };
