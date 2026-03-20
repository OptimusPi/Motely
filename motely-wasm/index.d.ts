// ── Analysis types ──

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

// ── Search types ──

export interface SearchCallbacks {
    onProgress?: (totalSeedsSearched: number, matchingSeeds: number, elapsedMs: number) => void;
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

export interface ValidateResult {
    valid: boolean;
    error: string;
}

// ── Capabilities ──

export interface CapabilitiesInfo {
    simd: boolean;
    threads: boolean;
    availableThreadCount: number;
    processorCount: number;
    runtime: string;
    version: string;
    timestamp: string;
}

// ── Instance ──

export interface MotelyInstance {
    readonly id: number;
    readonly isDestroyed: boolean;

    analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo>;

    startJamlSearch(jamlContent: string, options?: SequentialSearchOptions): Promise<string>;
    verifySeed(jamlContent: string, seed: string, options?: SearchRuntimeOptions): Promise<string>;
    startSeedListSearch(jamlContent: string, seeds: string[], options?: SearchRuntimeOptions): Promise<string>;
    startKeywordSearch(jamlContent: string, keyword: string, options?: KeywordSearchOptions): Promise<string>;
    startKeywordsSearch(jamlContent: string, keywords: string[], options?: KeywordSearchOptions): Promise<string>;
    startRandomSearch(jamlContent: string, count: number, options?: SearchRuntimeOptions): Promise<string>;
    startPalindromeSearch(jamlContent: string, options?: SearchRuntimeOptions): Promise<string>;
    stopSearch(): Promise<void>;

    destroy(): void;
}

// ── Runtime API ──

export interface MotelyWasmApi {
    createInstance(): MotelyInstance;

    // Global (no instance needed)
    getVersion(): Promise<string>;
    getCapabilities(): Promise<CapabilitiesInfo>;
    isSimdEnabled(): Promise<boolean>;
    getProcessorCount(): Promise<number>;
    validateJaml(jamlContent: string): Promise<ValidateResult>;

    // Backward compat (uses default instance)
    analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo>;
    startJamlSearch(jamlContent: string, options?: SequentialSearchOptions): Promise<string>;
    verifySeed(jamlContent: string, seed: string, options?: SearchRuntimeOptions): Promise<string>;
    startSeedListSearch(jamlContent: string, seeds: string[], options?: SearchRuntimeOptions): Promise<string>;
    startKeywordSearch(jamlContent: string, keyword: string, options?: KeywordSearchOptions): Promise<string>;
    startKeywordsSearch(jamlContent: string, keywords: string[], options?: KeywordSearchOptions): Promise<string>;
    startRandomSearch(jamlContent: string, count: number, options?: SearchRuntimeOptions): Promise<string>;
    startPalindromeSearch(jamlContent: string, options?: SearchRuntimeOptions): Promise<string>;
    stopSearch(): Promise<void>;
}

export interface LoadMotelyOptions {
    baseUrl?: string;
    threads?: "auto" | "on" | "off";
}

export declare function loadMotely(options?: LoadMotelyOptions): Promise<MotelyWasmApi>;
