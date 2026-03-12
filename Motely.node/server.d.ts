import type { CapabilitiesInfo, LoadMotelyOptions, MotelyNodeApi, SearchOptions, SeedAnalysisInfo } from './index.js';
export declare function getServerApi(options?: LoadMotelyOptions): Promise<MotelyNodeApi>;
export declare function disposeServerApi(): Promise<void>;
export declare function analyzeSeedServer(seed: string, deck: string, stake: string, options?: LoadMotelyOptions): Promise<SeedAnalysisInfo>;
export declare function startJamlSearchServer(jamlContent: string, options?: SearchOptions, loadOptions?: LoadMotelyOptions): Promise<import("./index.js").SearchResultInfo[]>;
export declare function getServerCapabilities(options?: LoadMotelyOptions): Promise<CapabilitiesInfo>;
export type { CapabilitiesInfo, LoadMotelyOptions, MotelyNodeApi, SearchOptions, SeedAnalysisInfo, } from './index.js';
