import { loadMotely } from './index.js';
let apiPromise = null;
let apiOptionsKey = null;
function getOptionsKey(options) {
    return JSON.stringify(options ?? {});
}
export function getServerApi(options) {
    const key = getOptionsKey(options);
    if (!apiPromise || apiOptionsKey !== key) {
        apiOptionsKey = key;
        apiPromise = loadMotely(options).catch((err) => {
            if (apiOptionsKey === key) {
                apiPromise = null;
                apiOptionsKey = null;
            }
            throw err;
        });
    }
    return apiPromise;
}
export async function disposeServerApi() {
    const current = apiPromise;
    apiPromise = null;
    apiOptionsKey = null;
    if (!current) {
        return;
    }
    const api = await current.catch(() => null);
    api?.dispose();
}
export async function analyzeSeedServer(seed, deck, stake, options) {
    const api = await getServerApi(options);
    return api.analyzeSeed(seed, deck, stake);
}
export async function startJamlSearchServer(jamlContent, options, loadOptions) {
    const api = await getServerApi(loadOptions);
    return api.startJamlSearch(jamlContent, options);
}
/** Search all palindrome seeds (e.g. ABCDDCBA, 12344321). */
export async function startPalindromeSearchServer(jamlContent, options, loadOptions) {
    const api = await getServerApi(loadOptions);
    return api.startJamlSearch(jamlContent, { ...options, palindrome: true });
}
/** Search all 8-char padded variations containing the keyword. */
export async function startKeywordSearchServer(jamlContent, keyword, padding, options, loadOptions) {
    const api = await getServerApi(loadOptions);
    return api.startJamlSearch(jamlContent, { ...options, keyword, ...(padding ? { padding } : {}) });
}
/** Search all 8-char padded variations for every keyword in the list. */
export async function startKeywordsSearchServer(jamlContent, keywords, padding, options, loadOptions) {
    const api = await getServerApi(loadOptions);
    return api.startJamlSearch(jamlContent, { ...options, keywords, ...(padding ? { padding } : {}) });
}
/** Search an explicit list of seeds. */
export async function startSeedListSearchServer(jamlContent, seeds, options, loadOptions) {
    const api = await getServerApi(loadOptions);
    return api.startJamlSearch(jamlContent, { ...options, seeds });
}
/** Verify a single specific seed against the filter. */
export async function verifySeedServer(jamlContent, seed, options, loadOptions) {
    const api = await getServerApi(loadOptions);
    return api.startJamlSearch(jamlContent, { ...options, specificSeed: seed });
}
/** Search N random seeds. */
export async function startRandomSearchServer(jamlContent, count, options, loadOptions) {
    const api = await getServerApi(loadOptions);
    return api.startJamlSearch(jamlContent, { ...options, randomSeeds: count });
}
export async function getServerCapabilities(options) {
    const api = await getServerApi(options);
    return api.getCapabilities();
}
