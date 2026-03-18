export interface VersionInfo {
    version: string;
    runtime: string;
    features: string[];
}
export interface CapabilitiesInfo {
    simd: boolean;
    threads: boolean;
    availableThreadCount: number;
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
    error?: string | null;
    antes: AnteAnalysisInfo[];
}
export interface SearchResultInfo {
    seed: string;
    score: number;
    tallies?: number[] | null;
}
export interface SearchStatusInfo {
    filterId: string;
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
export interface SearchCallbacks {
    /** Called with native primitives every ~15ms during search. No JSON overhead. */
    onProgress?: (totalSeedsSearched: number, matchingSeeds: number, elapsedMs: number, resultCount: number) => void;
    /** Called with native primitives for each new result found. No JSON overhead. */
    onResult?: (seed: string, score: number) => void;
}
export interface SearchRuntimeOptions extends SearchCallbacks {
    threadCount?: number;
    batchCharCount?: number;
}
export interface SequentialSearchOptions extends SearchRuntimeOptions {
    startBatch?: number;
    endBatch?: number;
}
export interface KeywordSearchOptions extends SearchRuntimeOptions {
    padding?: string;
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
    /** Get the number of threads the runtime can actually use */
    getAvailableThreadCount(): number;
    /** Get available processor count */
    getProcessorCount(): number;
    /**
     * Analyze a single seed. Returns full ante-by-ante breakdown.
     * @throws If the result contains an `error` field
     */
    analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo>;
    /**
     * Validate a JAML filter string.
     */
    validateJaml(jamlContent: string): Promise<ValidateResult>;
    /**
     * Start a JAML search. Returns a Promise that resolves with final SearchStatusInfo.
     * Progress is pushed to onProgress/onResult callbacks (native primitives, no JSON).
     * Only ONE search can run at a time. Starting a new search cancels any existing one.
     * @param jamlContent - The JAML filter content
     * @param options - Search parameters + onProgress/onResult callbacks
     * @returns Promise resolving to final search status with results
     */
    startJamlSearch(jamlContent: string, options?: SequentialSearchOptions): Promise<SearchStatusInfo>;
    verifySeed(jamlContent: string, seed: string, options?: SearchRuntimeOptions): Promise<SearchStatusInfo>;
    startSeedListSearch(jamlContent: string, seeds: string[], options?: SearchRuntimeOptions): Promise<SearchStatusInfo>;
    startKeywordSearch(jamlContent: string, keyword: string, options?: KeywordSearchOptions): Promise<SearchStatusInfo>;
    startKeywordsSearch(jamlContent: string, keywords: string[], options?: KeywordSearchOptions): Promise<SearchStatusInfo>;
    startRandomSearch(jamlContent: string, count: number, options?: SearchRuntimeOptions): Promise<SearchStatusInfo>;
    startPalindromeSearch(jamlContent: string, options?: SearchRuntimeOptions): Promise<SearchStatusInfo>;
    /** Stop the current running search (non-blocking, sets cancellation flag) */
    stopSearch(): void;
    /** Dispose the current search and free memory. Returns Promise. */
    disposeSearch(): Promise<void>;
    getLastNextState(): number;
    getLastGeneratedFirstPack(): boolean;
    getLastStateJson(): string;
    streamLuckyMoney(seed: string, deck: string, stake: string, state: number, take: number, baseLuck: number): Uint8Array;
    streamLuckyMult(seed: string, deck: string, stake: string, state: number, take: number, baseLuck: number): Uint8Array;
    streamMisprint(seed: string, deck: string, stake: string, state: number, take: number): Int32Array;
    streamCavendish(seed: string, deck: string, stake: string, state: number, take: number, baseLuck: number): Uint8Array;
    streamGrosMichel(seed: string, deck: string, stake: string, state: number, take: number, baseLuck: number): Uint8Array;
    streamErraticDeck(seed: string, deck: string, stake: string, state: number, take: number): string[];
    streamWheelOfFortune(seed: string, deck: string, stake: string, state: number, take: number, baseLuck: number): string[];
    streamTags(seed: string, deck: string, stake: string, ante: number, state: number, take: number): string[];
    streamBoosterPacks(seed: string, deck: string, stake: string, ante: number, state: number, generatedFirstPack: boolean, take: number): string[];
    streamVouchers(seed: string, deck: string, stake: string, ante: number, voucherBitfield: number, state: number, take: number): string[];
    streamTarot(seed: string, deck: string, stake: string, ante: number, source: string, stateJson: string, take: number): string[];
    streamPlanet(seed: string, deck: string, stake: string, ante: number, source: string, stateJson: string, take: number): string[];
    streamSpectral(seed: string, deck: string, stake: string, ante: number, source: string, stateJson: string, take: number): string[];
    streamStandardCards(seed: string, deck: string, stake: string, ante: number, flags: number, stateJson: string, take: number): string[];
    streamJokers(seed: string, deck: string, stake: string, ante: number, source: string, flags: number, stateJson: string, take: number): string[];
}
export interface LoadMotelyOptions {
    /** Base URL for runtime assets. Defaults to this package's bundled framework folder. */
    baseUrl?: string;
    /**
     * Threading mode.
     * - "auto": use threads when cross-origin isolation is available, otherwise fall back to the single-thread bundle
     * - "on": require the threaded bundle
     * - "off": force the single-thread bundle
     */
    threads?: "auto" | "on" | "off";
}
/**
 * Load the Motely WASM runtime and return the API.
 * Call once at app startup; the returned object is reusable.
 *
 * The _framework/dotnet.js entry point is the standard .NET WASM boot file.
 * See: https://github.com/dotnet/runtime/blob/main/src/mono/wasm/features.md
 */
export declare function loadMotely(options?: LoadMotelyOptions): Promise<MotelyWasmApi>;
