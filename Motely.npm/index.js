// Motely WASM Package Entry Point
// Thin loader that calls [JSExport] methods and parses JSON responses.
// The .NET WASM runtime is the source of truth; this is just the JS bridge.
function resolveFrameworkUrl(baseUrl, frameworkFolder) {
    if (baseUrl) {
        return (baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl) || baseUrl;
    }
    if (frameworkFolder === "_framework_st") {
        return new URL("./_framework_st/", import.meta.url).href.replace(/\/$/, "");
    }
    return new URL("./_framework/", import.meta.url).href.replace(/\/$/, "");
}
// ──────────────────────────────── Loader ────────────────────────────────
/**
 * Load the Motely WASM runtime and return the API.
 * Call once at app startup; the returned object is reusable.
 *
 * The _framework/dotnet.js entry point is the standard .NET WASM boot file.
 * See: https://github.com/dotnet/runtime/blob/main/src/mono/wasm/features.md
 */
export async function loadMotely(options) {
    // Diagnostic: warn if cross-origin isolation is missing (threads + SharedArrayBuffer require it)
    const supportsIsolation = typeof globalThis.crossOriginIsolated === "undefined" || globalThis.crossOriginIsolated;
    const threadingMode = options?.threads ?? "auto";
    if (!supportsIsolation && threadingMode !== "off") {
        console.warn("[motely-wasm] crossOriginIsolated is false. " +
            (threadingMode === "on"
                ? "Multi-threading and SharedArrayBuffer are REQUIRED by threads: \"on\" and will likely fail to initialize. "
                : "Multi-threading and SharedArrayBuffer are DISABLED. Falling back to the single-thread runtime bundle. ") +
            "Your server must send these headers on ALL responses:\n" +
            "  Cross-Origin-Opener-Policy: same-origin\n" +
            "  Cross-Origin-Embedder-Policy: require-corp\n" +
            "See: https://web.dev/articles/coop-coep");
    }
    const frameworkFolder = threadingMode === "off" || (!supportsIsolation && threadingMode === "auto")
        ? "_framework_st"
        : "_framework";
    const url = resolveFrameworkUrl(options?.baseUrl, frameworkFolder);
    const dotnetUrl = `${url}/dotnet.js`;
    // Dynamic import of the .NET WASM entry point.
    // @vite-ignore / webpackIgnore prevent bundlers from analyzing the URL.
    // With standard boot config (no WasmBundlerFriendlyBootConfig), dotnet.js uses fetch() for .wasm/.dat at runtime.
    const { dotnet } = await import(
    /* @vite-ignore */ /* webpackIgnore: true */ dotnetUrl);
    const runtime = await dotnet.create();
    const config = runtime.getConfig();
    const allExports = await runtime.getAssemblyExports(config.mainAssemblyName);
    const raw = allExports.Motely.BrowserWasm.MotelyWasmExports;
    runtime.runMain().catch(err => console.error("[motely-wasm] runMain failed:", err));
    const [versionJson, capabilitiesJson] = await Promise.all([
        raw.GetVersionAsync(),
        raw.GetCapabilitiesAsync(),
    ]);
    const cachedVersion = JSON.parse(versionJson);
    const cachedCapabilities = JSON.parse(capabilitiesJson);
    const api = {
        getVersion: () => cachedVersion,
        getCapabilities: () => cachedCapabilities,
        isSimdEnabled: () => cachedCapabilities.simd,
        isThreadingEnabled: () => cachedCapabilities.threads,
        getProcessorCount: () => cachedCapabilities.processorCount,
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
                threadCount: cachedCapabilities.processorCount,
                batchCharCount: 4,
                ...searchParams,
            };
            const optionsJson = JSON.stringify(withDefaults);
            const progressCb = onProgress
                ? (json) => {
                    const p = JSON.parse(json);
                    onProgress(p.seedsSearched, p.matchingSeeds, p.elapsedMs, p.resultCount);
                }
                : () => { };
            const resultCb = onResult ?? (() => { });
            const resultJson = await raw.StartJamlSearch(jamlContent, optionsJson, progressCb, resultCb);
            const result = JSON.parse(resultJson);
            if (result.error)
                throw new Error(result.error);
            return result;
        },
        stopSearch: () => { raw.StopSearch(); },
        disposeSearch: () => raw.DisposeSearch(),
    };
    return api;
}
