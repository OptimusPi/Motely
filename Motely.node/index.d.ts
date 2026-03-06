export interface VersionInfo {
    version: string;
    runtime: string;
    features: string[];
}
export interface CapabilitiesInfo {
    simd: boolean;
    threads: boolean;
    processorCount: number;
    runtime: string;
    version: string;
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
    shopQueue: ShopItemInfo[];
    packs: PackInfo[];
}
export interface SeedAnalysisInfo {
    seed: string;
    deck: string;
    stake: string;
    error?: string | null;
    antes: AnteAnalysisInfo[];
}
export interface SearchResultInfo {
    seed: string;
    score: number;
}
export interface ValidateResult {
    valid: boolean;
    error?: string | null;
    name?: string | null;
    deck?: string | null;
    stake?: string | null;
}
export interface SearchOptions {
    /** Thread count (default 1 — Node.js WASM is single-threaded) */
    threadCount?: number;
    /** Batch character count 1-7 (default 4) */
    batchCharCount?: number;
    randomSeeds?: number;
    cutoff?: string;
    startBatch?: number;
    endBatch?: number;
    specificSeed?: string;
    palindrome?: boolean;
    /** Called every ~2 seconds by the .NET search engine with current progress. */
    onProgress?: (seedsSearched: number, matchingSeeds: number, elapsedMs: number, resultCount: number) => void;
    /** Called each time a matching seed is found during search. */
    onResult?: (seed: string, score: number) => void;
}
export interface MotelyNodeApi {
    /** Get runtime capabilities (SIMD, threads, etc.) */
    getCapabilities(): Promise<CapabilitiesInfo>;
    /** Analyze a single seed. Returns full ante-by-ante breakdown. */
    analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo>;
    /** Validate a JAML filter string. */
    validateJaml(jamlContent: string): Promise<ValidateResult>;
    /**
     * Run a JAML search. Resolves with all matching seeds when search completes.
     * onProgress is called ~every 2s with stats; onResult is called per match as found.
     */
    startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<SearchResultInfo[]>;
    /** Stop any running search and release resources. */
    dispose(): void;
}
export interface LoadMotelyOptions {
    /** Path to the folder containing dotnet.js (default: package _framework). */
    frameworkPath?: string;
}
/**
 * Load the Motely WASM engine for Node.js. Call once at startup; reuse the returned API.
 */
export declare function loadMotely(options?: LoadMotelyOptions): Promise<MotelyNodeApi>;
