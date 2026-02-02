export interface DuckDbWasmResultsOptions {
    tableName?: string;
    maxQueueSize?: number;
}
export interface DuckDbWasmResultsHandle {
    getTopResults(limit?: number): Promise<Array<{
        seed: string;
        score: number;
        tallies?: string;
    }>>;
    getCount(): Promise<number>;
    close(): Promise<void>;
}
export declare function initDuckDbWasmResults(options?: DuckDbWasmResultsOptions): Promise<DuckDbWasmResultsHandle>;
