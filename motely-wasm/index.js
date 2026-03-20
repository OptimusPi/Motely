// motely-wasm — JS loader for .NET WASM runtime.
// Instance-based: loadMotely() → runtime, runtime.createInstance() → instance.
// Search uses push-based callbacks (unchanged). Analysis returns JSON.

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

    const noop3 = () => { };
    const noop2 = () => { };

    const resolveOpts = (opts) => ({
        threadCount: opts?.threadCount ?? 0,
        batchCharCount: opts?.batchCharCount ?? 0,
        onProgress: opts?.onProgress ?? noop3,
        onResult: opts?.onResult ?? noop2,
    });

    let cachedCapabilities = null;

    // ── Instance: the thing ──

    function createInstance() {
        const id = raw.CreateInstance();
        let destroyed = false;

        const inst = {
            get id() { return id; },
            get isDestroyed() { return destroyed; },

            // ── Analyze ──
            async analyzeSeed(seed, deck, stake) {
                const json = await raw.AnalyzeSeed(id, seed, deck, stake);
                return JSON.parse(json);
            },

            // ── Search (push-based, unchanged) ──
            async startJamlSearch(jamlContent, options) {
                const o = resolveOpts(options);
                return raw.StartJamlSearch(
                    id, jamlContent, o.threadCount, o.batchCharCount,
                    options?.startBatch ?? -1, options?.endBatch ?? -1,
                    o.onProgress, o.onResult
                );
            },

            async verifySeed(jamlContent, seed, options) {
                const o = resolveOpts(options);
                return raw.StartSeedListSearch(
                    id, jamlContent, o.threadCount, o.batchCharCount,
                    [seed], o.onProgress, o.onResult
                );
            },

            async startSeedListSearch(jamlContent, seeds, options) {
                const o = resolveOpts(options);
                return raw.StartSeedListSearch(
                    id, jamlContent, o.threadCount, o.batchCharCount,
                    seeds, o.onProgress, o.onResult
                );
            },

            async startKeywordSearch(jamlContent, keyword, options) {
                const o = resolveOpts(options);
                return raw.StartKeywordSearch(
                    id, jamlContent, o.threadCount, o.batchCharCount,
                    [keyword], options?.padding ?? "", o.onProgress, o.onResult
                );
            },

            async startKeywordsSearch(jamlContent, keywords, options) {
                const o = resolveOpts(options);
                return raw.StartKeywordSearch(
                    id, jamlContent, o.threadCount, o.batchCharCount,
                    keywords, options?.padding ?? "", o.onProgress, o.onResult
                );
            },

            async startRandomSearch(jamlContent, count, options) {
                const o = resolveOpts(options);
                return raw.StartRandomSearch(
                    id, jamlContent, o.threadCount, o.batchCharCount,
                    count, o.onProgress, o.onResult
                );
            },

            async startPalindromeSearch(jamlContent, options) {
                const o = resolveOpts(options);
                return raw.StartPalindromeSearch(
                    id, jamlContent, o.threadCount, o.batchCharCount,
                    o.onProgress, o.onResult
                );
            },

            async stopSearch() { await raw.StopSearch(id); },

            destroy() {
                if (!destroyed) {
                    destroyed = true;
                    raw.DestroyInstance(id);
                }
            },
        };

        return inst;
    }

    // ── Runtime: global + factory ──

    return {
        createInstance,

        // Global (no instance needed)
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

        async validateJaml(jamlContent) {
            const valid = await raw.ValidateJaml(jamlContent);
            const error = valid ? "" : await raw.ValidateJamlWithError(jamlContent);
            return { valid, error };
        },

        // ── Backward compat: flat methods that use a default instance ──
        _defaultInstance: null,
        _getDefault() {
            if (!this._defaultInstance || this._defaultInstance.isDestroyed) {
                this._defaultInstance = createInstance();
            }
            return this._defaultInstance;
        },
        analyzeSeed(seed, deck, stake) { return this._getDefault().analyzeSeed(seed, deck, stake); },
        startJamlSearch(jamlContent, options) { return this._getDefault().startJamlSearch(jamlContent, options); },
        verifySeed(jamlContent, seed, options) { return this._getDefault().verifySeed(jamlContent, seed, options); },
        startSeedListSearch(jamlContent, seeds, options) { return this._getDefault().startSeedListSearch(jamlContent, seeds, options); },
        startKeywordSearch(jamlContent, keyword, options) { return this._getDefault().startKeywordSearch(jamlContent, keyword, options); },
        startKeywordsSearch(jamlContent, keywords, options) { return this._getDefault().startKeywordsSearch(jamlContent, keywords, options); },
        startRandomSearch(jamlContent, count, options) { return this._getDefault().startRandomSearch(jamlContent, count, options); },
        startPalindromeSearch(jamlContent, options) { return this._getDefault().startPalindromeSearch(jamlContent, options); },
        stopSearch() { return this._getDefault().stopSearch(); },
    };
}
