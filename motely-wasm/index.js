// Motely WASM Package Entry Point
// Thin loader that calls [JSExport] methods and parses JSON responses.
// The .NET WASM runtime is the source of truth; this is just the JS bridge.
function resolveFrameworkUrl(baseUrl, frameworkFolder) {
    if (baseUrl) {
        return (baseUrl.endsWith("/") ? baseUrl.slice(0, -1) : baseUrl) || baseUrl;
    }
    if (frameworkFolder === "_framework_st") {
        return new URL("./_framework_st/dotnet.js", import.meta.url).href.replace(/\/dotnet\.js$/, "");
    }
    return new URL("./_framework/dotnet.js", import.meta.url).href.replace(/\/dotnet\.js$/, "");
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
    const cachedVersion = {
        version: raw.GetVersion(),
        runtime: raw.GetRuntime(),
        features: raw.GetFeatureList(),
    };
    const cachedCapabilities = {
        simd: raw.IsSimdEnabled(),
        threads: raw.IsThreadingEnabled(),
        availableThreadCount: raw.GetAvailableThreadCount(),
        processorCount: raw.GetProcessorCount(),
        runtime: cachedVersion.runtime,
        version: cachedVersion.version,
        timestamp: new Date().toISOString(),
    };
    const startSearch = async (jamlContent, searchParams, callbacks) => {
        const { onProgress, onResult } = callbacks ?? {};
        const withDefaults = {
            threadCount: cachedCapabilities.processorCount,
            batchCharCount: 4,
            ...searchParams,
        };
        const optionsJson = JSON.stringify(withDefaults);
        let resultCount = 0;
        const progressCb = onProgress
            ? (totalSeedsSearched, matchingSeeds, elapsedMs) => {
                onProgress(totalSeedsSearched, matchingSeeds, elapsedMs, resultCount);
            }
            : () => { };
        const resultCb = (seed, score) => {
            resultCount++;
            onResult?.(seed, score);
        };
        const resultJson = await raw.StartJamlSearch(jamlContent, optionsJson, progressCb, resultCb);
        const result = JSON.parse(resultJson);
        if (result.error)
            throw new Error(result.error);
        return result;
    };
    const api = {
        getVersion: () => cachedVersion,
        getCapabilities: () => cachedCapabilities,
        isSimdEnabled: () => cachedCapabilities.simd,
        isThreadingEnabled: () => cachedCapabilities.availableThreadCount > 1,
        getAvailableThreadCount: () => cachedCapabilities.availableThreadCount,
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
            return startSearch(jamlContent, {
                threadCount: options?.threadCount,
                batchCharCount: options?.batchCharCount,
                startBatch: options?.startBatch,
                endBatch: options?.endBatch,
            }, options);
        },
        async verifySeed(jamlContent, seed, options) {
            return startSearch(jamlContent, {
                threadCount: options?.threadCount,
                batchCharCount: options?.batchCharCount,
                specificSeed: seed,
            }, options);
        },
        async startSeedListSearch(jamlContent, seeds, options) {
            return startSearch(jamlContent, {
                threadCount: options?.threadCount,
                batchCharCount: options?.batchCharCount,
                seeds,
            }, options);
        },
        async startKeywordSearch(jamlContent, keyword, options) {
            return startSearch(jamlContent, {
                threadCount: options?.threadCount,
                batchCharCount: options?.batchCharCount,
                keywords: [keyword],
                padding: options?.padding,
            }, options);
        },
        async startKeywordsSearch(jamlContent, keywords, options) {
            return startSearch(jamlContent, {
                threadCount: options?.threadCount,
                batchCharCount: options?.batchCharCount,
                keywords,
                padding: options?.padding,
            }, options);
        },
        async startRandomSearch(jamlContent, count, options) {
            return startSearch(jamlContent, {
                threadCount: options?.threadCount,
                batchCharCount: options?.batchCharCount,
                randomSeeds: count,
            }, options);
        },
        async startPalindromeSearch(jamlContent, options) {
            return startSearch(jamlContent, {
                threadCount: options?.threadCount,
                batchCharCount: options?.batchCharCount,
                palindrome: true,
            }, options);
        },
        stopSearch: () => { raw.StopSearch(); },
        disposeSearch: () => raw.DisposeSearch(),
        getLastNextState: () => raw.GetLastNextState(),
        getLastGeneratedFirstPack: () => raw.GetLastGeneratedFirstPack(),
        getLastStateJson: () => raw.GetLastStateJson(),
        streamLuckyMoney: (seed, deck, stake, state, take, baseLuck) => raw.StreamLuckyMoney(seed, deck, stake, state, take, baseLuck),
        streamLuckyMult: (seed, deck, stake, state, take, baseLuck) => raw.StreamLuckyMult(seed, deck, stake, state, take, baseLuck),
        streamMisprint: (seed, deck, stake, state, take) => raw.StreamMisprint(seed, deck, stake, state, take),
        streamCavendish: (seed, deck, stake, state, take, baseLuck) => raw.StreamCavendish(seed, deck, stake, state, take, baseLuck),
        streamGrosMichel: (seed, deck, stake, state, take, baseLuck) => raw.StreamGrosMichel(seed, deck, stake, state, take, baseLuck),
        streamErraticDeck: (seed, deck, stake, state, take) => raw.StreamErraticDeck(seed, deck, stake, state, take),
        streamWheelOfFortune: (seed, deck, stake, state, take, baseLuck) => raw.StreamWheelOfFortune(seed, deck, stake, state, take, baseLuck),
        streamTags: (seed, deck, stake, ante, state, take) => raw.StreamTags(seed, deck, stake, ante, state, take),
        streamBoosterPacks: (seed, deck, stake, ante, state, generatedFirstPack, take) => raw.StreamBoosterPacks(seed, deck, stake, ante, state, generatedFirstPack, take),
        streamVouchers: (seed, deck, stake, ante, voucherBitfield, state, take) => raw.StreamVouchers(seed, deck, stake, ante, voucherBitfield, state, take),
        streamTarot: (seed, deck, stake, ante, source, stateJson, take) => raw.StreamTarot(seed, deck, stake, ante, source, stateJson, take),
        streamPlanet: (seed, deck, stake, ante, source, stateJson, take) => raw.StreamPlanet(seed, deck, stake, ante, source, stateJson, take),
        streamSpectral: (seed, deck, stake, ante, source, stateJson, take) => raw.StreamSpectral(seed, deck, stake, ante, source, stateJson, take),
        streamStandardCards: (seed, deck, stake, ante, flags, stateJson, take) => raw.StreamStandardCards(seed, deck, stake, ante, flags, stateJson, take),
        streamJokers: (seed, deck, stake, ante, source, flags, stateJson, take) => raw.StreamJokers(seed, deck, stake, ante, source, flags, stateJson, take),
    };
    return api;
}
