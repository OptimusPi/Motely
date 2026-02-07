// Motely WASM Package Entry Point
// Thin loader that calls [JSExport] methods and parses JSON responses.
// The .NET WASM runtime is the source of truth; this is just the JS bridge.
/**
 * Load the Motely WASM runtime and return the API.
 * Call once at app startup; the returned object is reusable.
 *
 * The _framework/dotnet.js entry point is the standard .NET WASM boot file.
 * See: https://github.com/dotnet/runtime/blob/main/src/mono/wasm/features.md
 */
export async function loadMotely(options) {
    // Install no-op callbacks before the runtime boots so [JSImport] bindings resolve
    globalThis.__motelyOnProgress = () => { };
    globalThis.__motelyOnResult = () => { };
    const defaultBase = "/_framework";
    const base = (options?.baseUrl ?? defaultBase).replace(/\/$/, "") || defaultBase;
    const origin = typeof window !== "undefined" ? window.location.origin : "https://localhost";
    const url = base.startsWith("http") ? base : new URL(base, origin).href;
    const dotnetUrl = `${url}/dotnet.js`;
    // Dynamic import of the .NET WASM entry point. @vite-ignore / webpackIgnore prevent bundlers from analyzing the URL.
    // With standard boot config, dotnet.js uses fetch() for .wasm/.dat at runtime.
    const { dotnet } = await import(/* @vite-ignore */ /* webpackIgnore: true */ dotnetUrl);
    const runtime = await dotnet.create();
    const config = runtime.getConfig();
    const allExports = await runtime.getAssemblyExports(config.mainAssemblyName);
    const raw = allExports.Motely.BrowserWasm.MotelyWasmExports;
    runtime.runMain().catch(err => console.error("[motely-wasm] runMain failed:", err));
    const api = {
        getVersion: () => JSON.parse(raw.GetVersion()),
        getCapabilities: () => JSON.parse(raw.GetCapabilities()),
        isSimdEnabled: () => raw.IsSimdEnabled(),
        isThreadingEnabled: () => raw.IsThreadingEnabled(),
        getProcessorCount: () => raw.GetProcessorCount(),
        analyzeSeed(seed, deck, stake) {
            const json = raw.AnalyzeSeed(seed, deck, stake);
            const result = JSON.parse(json);
            if (result.error && !result.seed)
                throw new Error(result.error);
            return result;
        },
        validateJaml: (jaml) => JSON.parse(raw.ValidateJaml(jaml)),
        async startJamlSearch(jamlContent, options) {
            const { onProgress, onResult, ...searchParams } = options ?? {};
            globalThis.__motelyOnProgress = onProgress ?? (() => { });
            globalThis.__motelyOnResult = onResult ?? (() => { });
            const optionsJson = Object.keys(searchParams).length > 0
                ? JSON.stringify(searchParams) : "{}";
            const resultJson = await raw.StartJamlSearch(jamlContent, optionsJson);
            globalThis.__motelyOnProgress = () => { };
            globalThis.__motelyOnResult = () => { };
            const result = JSON.parse(resultJson);
            if (result.error && !result.searchId)
                throw new Error(result.error);
            return result;
        },
        getSearchStatus(searchId, resultLimit) {
            const json = raw.GetSearchStatus(searchId, resultLimit ?? 50);
            const result = JSON.parse(json);
            if (result.error && !result.searchId)
                throw new Error(result.error);
            return result;
        },
        stopSearch: (searchId) => raw.StopSearch(searchId),
        disposeSearch: (searchId) => raw.DisposeSearch(searchId),
    };
    return api;
}
