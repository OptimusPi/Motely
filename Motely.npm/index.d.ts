export interface VersionInfo {
    version: string;
    runtime: string;
    framework: string;
}
export interface CapabilitiesInfo {
    simd: boolean;
    threads: boolean;
    processorCount: number;
    runtime: string;
    version: string;
    timestamp: string;
    note?: string | null;
}
export interface SearchResultInfo {
    seed: string;
    score: number;
    tallies?: number[] | null;
}
export interface SearchStatusInfo {
    searchId: string;
    status: string;
    isRunning: boolean;
    progressPercent: number;
    totalSeedsSearched: number;
    matchingSeeds: number;
    resultCount: number;
    results: SearchResultInfo[];
    error?: string | null;
}
export interface ShopItemInfo {
    id: string;
    name: string;
}
export interface PackInfo {
    type: string;
    items: string[];
}
export interface AnteAnalysisInfo {
    ante: number;
    boss: string;
    voucher: string;
    smallBlindTag: string;
    bigBlindTag: string;
    drawOrder: string;
    shopQueue: ShopItemInfo[];
    packs: PackInfo[];
}
export interface SeedAnalysisInfo {
    seed: string;
    deck: string;
    stake: string;
    erraticDeckComposition: string[];
    twos: number;
    error?: string | null;
    antes: AnteAnalysisInfo[];
}
export interface MotelyWasmApi {
    GetVersion(): VersionInfo;
    GetCapabilities(): CapabilitiesInfo;
    IsSimdEnabled(): boolean;
    IsThreadingEnabled(): boolean;
    GetProcessorCount(): number;
    AnalyzeSeed(seed: string, deck: string, stake: string, ante: number, shop: number, config: string): SeedAnalysisInfo;
    StartJamlSearch(jamlContent: string, deck?: string, stake?: string, threads?: number, batchSize?: number, startBatch?: number, endBatch?: number, cutoff?: number): string;
    GetSearchStatus(searchId: string, limit?: number): SearchStatusInfo;
    StopSearch(searchId: string): void;
}
export interface LoadMotelyOptions {
    /** Base URL for _framework (e.g. "/_framework" or "https://cdn.example/assets"). Default "/_framework". */
    baseUrl?: string;
}
export declare function loadMotely(options?: LoadMotelyOptions): Promise<MotelyWasmApi>;
