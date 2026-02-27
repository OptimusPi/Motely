export interface ValidateResult {
    valid: boolean;
    error?: string;
    name?: string;
    deck?: string;
    stake?: string;
}

export interface ShopItem {
    id: string;
    name: string;
}

export interface Pack {
    type: string;
    items: string[];
}

export interface AnteAnalysis {
    ante: number;
    boss: string;
    voucher: string;
    smallBlindTag: string;
    bigBlindTag: string;
    shopQueue: ShopItem[];
    packs: Pack[];
}

export interface SeedAnalysis {
    seed: string;
    deck: string;
    stake: string;
    error?: string;
    antes: AnteAnalysis[];
}

export interface Capabilities {
    runtime: string;
    simd: boolean;
    threads: boolean;
    processorCount: number;
    version: string;
}

export interface LoadOptions {
    /** WASI runtime: 'wasmtime' | 'wasmer' | 'node' (default: 'wasmtime') */
    runtime?: 'wasmtime' | 'wasmer' | 'node';
    /** Path to the .wasm binary (default: bundled) */
    wasmPath?: string;
}

export declare class MotelyWasi {
    static load(opts?: LoadOptions): Promise<MotelyWasi>;
    validateJaml(jaml: string): Promise<ValidateResult>;
    analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysis>;
    getCapabilities(): Promise<Capabilities>;
    close(): void;
}
