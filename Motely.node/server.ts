import { loadMotely } from './index.js';
import type {
  CapabilitiesInfo,
  LoadMotelyOptions,
  MotelyNodeApi,
  SearchOptions,
  SeedAnalysisInfo,
} from './index.js';

let apiPromise: Promise<MotelyNodeApi> | null = null;
let apiOptionsKey: string | null = null;

function getOptionsKey(options?: LoadMotelyOptions): string {
  return JSON.stringify(options ?? {});
}

export function getServerApi(options?: LoadMotelyOptions): Promise<MotelyNodeApi> {
  const key = getOptionsKey(options);

  if (!apiPromise || apiOptionsKey !== key) {
    apiOptionsKey = key;
    apiPromise = loadMotely(options).catch((err: unknown) => {
      if (apiOptionsKey === key) {
        apiPromise = null;
        apiOptionsKey = null;
      }
      throw err;
    });
  }

  return apiPromise;
}

export async function disposeServerApi(): Promise<void> {
  const current = apiPromise;
  apiPromise = null;
  apiOptionsKey = null;

  if (!current) {
    return;
  }

  const api = await current.catch(() => null);
  api?.dispose();
}

export async function analyzeSeedServer(
  seed: string,
  deck: string,
  stake: string,
  options?: LoadMotelyOptions,
): Promise<SeedAnalysisInfo> {
  const api = await getServerApi(options);
  return api.analyzeSeed(seed, deck, stake);
}

export async function startJamlSearchServer(
  jamlContent: string,
  options?: SearchOptions,
  loadOptions?: LoadMotelyOptions,
) {
  const api = await getServerApi(loadOptions);
  return api.startJamlSearch(jamlContent, options);
}

export async function getServerCapabilities(options?: LoadMotelyOptions): Promise<CapabilitiesInfo> {
  const api = await getServerApi(options);
  return api.getCapabilities();
}

export type {
  CapabilitiesInfo,
  LoadMotelyOptions,
  MotelyNodeApi,
  SearchOptions,
  SeedAnalysisInfo,
} from './index.js';
