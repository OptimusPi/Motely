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
export interface SearchProgressInfo {
    type: 'progress';
    seedsSearched: number;
    matchingSeeds: number;
    elapsedMs: number;
    resultCount: number;
}
export interface SearchCompleteInfo {
    type: 'done';
    searchId: string;
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
    onProgress?: (seedsSearched: number, matchingSeeds: number, elapsedMs: number, resultCount: number) => void;
    onResult?: (seed: string, score: number) => void;
}
export interface ErrorResult {
    error: string;
}
export interface MotelyNodeApi {
    /** Get runtime capabilities (SIMD, threads, etc.) */
    getCapabilities(): Promise<CapabilitiesInfo>;
    /** Analyze a single seed. Returns full ante-by-ante breakdown. */
    analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo>;
    /** Validate a JAML filter string. */
    validateJaml(jamlContent: string): Promise<ValidateResult>;
    /**
     * Start a JAML search. Returns a Promise that resolves when search completes.
     * Progress is pushed to onProgress/onResult callbacks.
     */
    startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<void>;
    /** Dispose and cleanup */
    dispose(): void;
}
declare global {
    var __motelyOnProgress: (totalSeedsSearched: number, matchingSeeds: number, elapsedMs: number, resultCount: number) => void;
    var __motelyOnResult: (seed: string, score: number) => void;
}
export interface LoadMotelyOptions {
    /** When set, load the .NET addon via node-api-dotnet (in-process). Requires dependency "node-api-dotnet" and a built Motely.NodeAddon.dll. */
    addonPath?: string;
    /** When using WASM, path to the folder containing dotnet.js (default: package _framework). */
    frameworkPath?: string;
}
/**
 * Load Motely for Node.js. Prefer node-api-dotnet addon when addonPath is set; otherwise use WASM.
 * Call once at app startup; the returned object is reusable.
 * @see https://microsoft.github.io/node-api-dotnet/reference/js/
 */
export declare function loadMotely(options?: LoadMotelyOptions): Promise<MotelyNodeApi>;
