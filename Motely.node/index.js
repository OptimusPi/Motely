// Motely Node.js Package — WASM-based Balatro seed engine
// Wraps .NET WASM exports (Motely.SingleThread / Motely.BrowserWasm)
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { pathToFileURL } from 'node:url';
function _dir() {
    return dirname(fileURLToPath(import.meta.url));
}
function buildApi(raw, cachedCapabilities, pollIntervalMs) {
    let disposed = false;
    return {
        async getCapabilities() {
            return cachedCapabilities;
        },
        getAvailableThreadCount() {
            return cachedCapabilities.availableThreadCount;
        },
        async analyzeSeed(seed, deck, stake) {
            const json = await raw.analyzeSeedAsync(seed, deck, stake);
            const result = JSON.parse(json);
            if (result.error && !result.seed)
                throw new Error(result.error);
            return result;
        },
        async validateJaml(jaml) {
            const json = await raw.validateJamlAsync(jaml);
            return JSON.parse(json);
        },
        async startJamlSearch(jamlContent, options) {
            if (disposed) {
                throw new Error('Motely instance has been disposed');
            }
            const { onProgress, onResult, ...searchParams } = options ?? {};
            const optionsJson = JSON.stringify({
                threadCount: Math.max(1, searchParams.threadCount ?? cachedCapabilities.availableThreadCount ?? 1),
                batchCharCount: 4,
                palindrome: searchParams.palindrome ?? !(searchParams.specificSeed || searchParams.randomSeeds),
                ...searchParams,
            });
            const results = [];
            const seen = new Set();
            const applyStatus = (status) => {
                onProgress?.(status.totalSeedsSearched, status.matchingSeeds, status.elapsedMs, status.resultCount);
                for (const result of status.results ?? []) {
                    const key = `${result.seed}:${result.score}`;
                    if (seen.has(key))
                        continue;
                    seen.add(key);
                    results.push(result);
                    onResult?.(result.seed, result.score);
                }
            };
            const completionPromise = raw.startJamlSearch(jamlContent, optionsJson)
                .then(json => ({ kind: 'done', json }))
                .catch(error => ({ kind: 'error', error }));
            while (true) {
                const next = await Promise.race([
                    completionPromise,
                    new Promise(resolve => setTimeout(() => resolve({ kind: 'tick' }), pollIntervalMs)),
                ]);
                if (next.kind === 'error') {
                    throw next.error;
                }
                if (next.kind === 'done') {
                    const status = JSON.parse(next.json);
                    if (status.error)
                        throw new Error(status.error);
                    applyStatus(status);
                    return results;
                }
                const statusJson = await raw.getSearchStatus();
                const status = JSON.parse(statusJson);
                if (status.error) {
                    if (status.error === 'No active search')
                        continue;
                    throw new Error(status.error);
                }
                applyStatus(status);
            }
        },
        dispose() {
            disposed = true;
            try {
                raw.stopSearch();
            }
            catch {
            }
            void raw.disposeSearch();
        },
    };
}
/**
 * Load the Motely WASM engine for Node.js. Call once at startup; reuse the returned API.
 */
export async function loadMotely(options) {
    const addonModulePath = options?.addonPath
        ?? (options?.frameworkPath
            ? join(options.frameworkPath, 'Motely.NodeAddon.mjs')
            : join(_dir(), 'addon', 'Motely.NodeAddon.mjs'));
    const addonUrl = pathToFileURL(addonModulePath).href;
    const mod = await import(addonUrl);
    const raw = mod.MotelyNodeExports;
    const capabilitiesJson = await raw.getCapabilitiesAsync();
    const cachedCapabilities = JSON.parse(capabilitiesJson);
    return buildApi(raw, cachedCapabilities, Math.max(25, options?.pollIntervalMs ?? 100));
}
