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
export async function getServerCapabilities(options) {
    const api = await getServerApi(options);
    return api.getCapabilities();
}
