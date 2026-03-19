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

// ── API ──

export interface MotelyWasmApi {
    getVersion(): Promise<string>;
    getCapabilities(): Promise<CapabilitiesInfo>;
    isSimdEnabled(): Promise<boolean>;
    getProcessorCount(): Promise<number>;

    analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo>;
    validateJaml(jamlContent: string): Promise<ValidateResult>;

    startJamlSearch(jamlContent: string, options?: SequentialSearchOptions): Promise<string>;
    verifySeed(jamlContent: string, seed: string, options?: SearchRuntimeOptions): Promise<string>;
    startSeedListSearch(jamlContent: string, seeds: string[], options?: SearchRuntimeOptions): Promise<string>;
    startKeywordSearch(jamlContent: string, keyword: string, options?: KeywordSearchOptions): Promise<string>;
    startKeywordsSearch(jamlContent: string, keywords: string[], options?: KeywordSearchOptions): Promise<string>;
    startRandomSearch(jamlContent: string, count: number, options?: SearchRuntimeOptions): Promise<string>;
    startPalindromeSearch(jamlContent: string, options?: SearchRuntimeOptions): Promise<string>;
    stopSearch(): void;
}

export interface LoadMotelyOptions {
    baseUrl?: string;
    threads?: "auto" | "on" | "off";
}

export declare function loadMotely(options?: LoadMotelyOptions): Promise<MotelyWasmApi>;
