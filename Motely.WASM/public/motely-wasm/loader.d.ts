import type { MotelyWasmApi, SeedAnalysisResult, AnteAnalysis, ShopItem, Pack, SearchResponse, SearchHit, SearchProgress, ValidateResult, VersionInfo, ErrorResult } from '../motely-wasm';
import { type DuckDbWasmResultsHandle, type DuckDbWasmResultsOptions } from './duckdb';
/**
 * Loads the Motely WASM runtime from the specified base URL.
 * Works with Next.js and other bundlers by using a script tag injection approach.
 *
 * @param baseUrl The public path where the motely-wasm files are served (e.g., '/motely-wasm' or 'https://cdn.example.com/motely').
 * @returns A promise that resolves to the MotelyWasmApi.
 */
export declare function loadMotely(baseUrl?: string): Promise<MotelyWasmApi>;
/**
 * Initialize DuckDB-WASM results storage and hook into MotelyWasmOnResult.
 * Call this before starting a search if you want browser-side results persisted in DuckDB-WASM.
 */
export declare function initDuckDbWasmResults(options?: DuckDbWasmResultsOptions): Promise<DuckDbWasmResultsHandle>;
export type { MotelyWasmApi, SeedAnalysisResult, AnteAnalysis, ShopItem, Pack, SearchResponse, SearchHit, SearchProgress, ValidateResult, VersionInfo, ErrorResult, };
export type { DuckDbWasmResultsHandle, DuckDbWasmResultsOptions };
