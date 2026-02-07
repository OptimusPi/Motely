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
    timestamp: string;
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
export interface SearchResultInfo {
    seed: string;
    score: number;
    tallies?: number[] | null;
}
export interface SearchStatusInfo {
    searchId: string;
    status: string;
    isRunning: boolean;
    totalSeedsSearched: number;
    matchingSeeds: number;
    resultCount: number;
    elapsedMs: number;
    results: SearchResultInfo[];
    error?: string | null;
}
export interface ValidateResult {
    valid: boolean;
    error?: string | null;
    name?: string | null;
    deck?: string | null;
    stake?: string | null;
}
export interface SearchOptions {
    threadCount?: number;
    batchSize?: number;
    startBatch?: number;
    endBatch?: number;
    cutoff?: string;
    specificSeed?: string;
    randomSeeds?: number;
    palindrome?: boolean;
}
export interface ErrorResult {
    error: string;
}
export interface MotelyWasmApi {
    /** Get version and feature info */
    getVersion(): VersionInfo;
    /** Get runtime capabilities (SIMD, threads, etc.) */
    getCapabilities(): CapabilitiesInfo;
    /** Check if SIMD is hardware-accelerated */
    isSimdEnabled(): boolean;
    /** Check if threading is enabled */
    isThreadingEnabled(): boolean;
    /** Get available processor count */
    getProcessorCount(): number;
    /**
     * Analyze a single seed. Returns full ante-by-ante breakdown.
     * @throws If the result contains an `error` field
     */
    analyzeSeed(seed: string, deck: string, stake: string): SeedAnalysisInfo;
    /**
     * Validate a JAML filter string.
     */
    validateJaml(jamlContent: string): ValidateResult;
    /**
     * Start a JAML search. Returns a searchId for polling.
     * @param jamlContent - The JAML filter content
     * @param options - Optional search parameters
     * @returns searchId string
     */
    startJamlSearch(jamlContent: string, options?: SearchOptions): string;
    /**
     * Get status and top results for an active search.
     * @param searchId - The search ID returned by startJamlSearch
     * @param resultLimit - Max results to include (default 50)
     */
    getSearchStatus(searchId: string, resultLimit?: number): SearchStatusInfo;
    /** Stop a running search */
    stopSearch(searchId: string): void;
    /** Dispose a completed/stopped search and free memory */
    disposeSearch(searchId: string): void;
}
export interface LoadMotelyOptions {
    /** Base URL for _framework (e.g. "/_framework" or "https://cdn.example/assets"). Default "/_framework". */
    baseUrl?: string;
}
/**
 * Load the Motely WASM runtime and return the API.
 * Call once at app startup; the returned object is reusable.
 */
export declare function loadMotely(options?: LoadMotelyOptions): Promise<MotelyWasmApi>;
