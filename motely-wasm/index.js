// motely-wasm — JS loader for .NET WASM [JSExport] methods.
// No JSON serialization for search or getters. AnalyzeSeed returns JSON (complex nested data).

function resolveFrameworkUrl(baseUrl, frameworkFolder) {
    if (baseUrl) {
        const base = baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl;
        return frameworkFolder === "_framework_st" ? `${base}-st` : `${base}/${frameworkFolder}`;
    }
    return new URL(`./${frameworkFolder}/dotnet.js`, import.meta.url).href.replace(/\/dotnet\.js$/, "");
}

export async function loadMotely(options) {
    const supportsIsolation = typeof globalThis.crossOriginIsolated === "undefined" || globalThis.crossOriginIsolated;
    const threadingMode = options?.threads ?? "auto";

    if (!supportsIsolation && threadingMode !== "off") {
        console.warn(
            "[motely-wasm] crossOriginIsolated is false. " +
            (threadingMode === "on"
                ? "Threading REQUIRED but SharedArrayBuffer unavailable. Will likely fail."
                : "Falling back to single-thread runtime.") +
            "\nServer must send: Cross-Origin-Opener-Policy: same-origin, Cross-Origin-Embedder-Policy: require-corp"
        );
    }

    const frameworkFolder = threadingMode === "off" || (!supportsIsolation && threadingMode === "auto")
        ? "_framework_st"
        : "_framework";

    const url = resolveFrameworkUrl(options?.baseUrl, frameworkFolder);
    const { dotnet } = await import(/* @vite-ignore */ /* webpackIgnore: true */ `${url}/dotnet.js`);
    const runtime = await dotnet.create();
    const config = runtime.getConfig();
    const allExports = await runtime.getAssemblyExports(config.mainAssemblyName);
    const raw = allExports.Motely.BrowserWasm.MotelyWasmExports;

    runtime.runMain().catch(err => console.error("[motely-wasm] runMain failed:", err));

    // No-op callbacks for when caller doesn't provide them
    const noop3 = () => { };
    const noop2 = () => { };

    const resolveOpts = (opts) => ({
        threadCount: opts?.threadCount ?? 0,
        batchCharCount: opts?.batchCharCount ?? 0,
        onProgress: opts?.onProgress ?? noop3,
        onResult: opts?.onResult ?? noop2,
    });

    let cachedCapabilities = null;

    const api = {
        // ── Getters (async — dispatched to worker thread) ──
        getVersion: () => raw.GetVersion(),
        async getCapabilities() {
            if (cachedCapabilities) return cachedCapabilities;
            const [version, simd, processorCount] = await Promise.all([
                raw.GetVersion(), raw.IsSimdEnabled(), raw.GetProcessorCount(),
            ]);
            cachedCapabilities = {
                simd,
                threads: typeof SharedArrayBuffer !== "undefined",
                availableThreadCount: processorCount,
                processorCount,
                runtime: "dotnet-wasm",
                version,
                timestamp: new Date().toISOString(),
            };
            return cachedCapabilities;
        },
        isSimdEnabled: () => raw.IsSimdEnabled(),
        getProcessorCount: () => raw.GetProcessorCount(),

        // ── Analyze (JSON — complex nested data) ──
        async analyzeSeed(seed, deck, stake) {
            const json = await raw.AnalyzeSeed(seed, deck, stake);
            return JSON.parse(json);
        },

        // ── Validate (no JSON) ──
        async validateJaml(jamlContent) {
            const valid = await raw.ValidateJaml(jamlContent);
            const error = valid ? "" : await raw.ValidateJamlWithError(jamlContent);
            return { valid, error };
        },

        // ── Search methods (no JSON — typed params + callbacks) ──
        async startJamlSearch(jamlContent, options) {
            const o = resolveOpts(options);
            return raw.StartJamlSearch(
                jamlContent, o.threadCount, o.batchCharCount,
                options?.startBatch ?? -1, options?.endBatch ?? -1,
                o.onProgress, o.onResult
            );
        },

        async verifySeed(jamlContent, seed, options) {
            const o = resolveOpts(options);
            return raw.StartSeedListSearch(
                jamlContent, o.threadCount, o.batchCharCount,
                [seed], o.onProgress, o.onResult
            );
        },

        async startSeedListSearch(jamlContent, seeds, options) {
            const o = resolveOpts(options);
            return raw.StartSeedListSearch(
                jamlContent, o.threadCount, o.batchCharCount,
                seeds, o.onProgress, o.onResult
            );
        },

        async startKeywordSearch(jamlContent, keyword, options) {
            const o = resolveOpts(options);
            return raw.StartKeywordSearch(
                jamlContent, o.threadCount, o.batchCharCount,
                [keyword], options?.padding ?? "", o.onProgress, o.onResult
            );
        },

        async startKeywordsSearch(jamlContent, keywords, options) {
            const o = resolveOpts(options);
            return raw.StartKeywordSearch(
                jamlContent, o.threadCount, o.batchCharCount,
                keywords, options?.padding ?? "", o.onProgress, o.onResult
            );
        },

        async startRandomSearch(jamlContent, count, options) {
            const o = resolveOpts(options);
            return raw.StartRandomSearch(
                jamlContent, o.threadCount, o.batchCharCount,
                count, o.onProgress, o.onResult
            );
        },

        async startPalindromeSearch(jamlContent, options) {
            const o = resolveOpts(options);
            return raw.StartPalindromeSearch(
                jamlContent, o.threadCount, o.batchCharCount,
                o.onProgress, o.onResult
            );
        },

        stopSearch: async () => await raw.StopSearch(),
    };

    return api;
}
