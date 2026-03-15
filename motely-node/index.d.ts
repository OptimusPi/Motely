/** Runtime capabilities (SIMD, threads, etc.). */
export interface CapabilitiesInfo {
  availableThreadCount?: number;
  processorCount?: number;
  simd?: boolean;
  threads?: boolean;
  runtime?: string;
  version?: string;
  timestamp?: string;
  [key: string]: unknown;
}

/** Options for startJamlSearch. */
export interface SearchOptions {
  threadCount?: number;
  batchCharCount?: number;
  startBatch?: number;
  endBatch?: number;
  cutoff?: string | number;
  specificSeed?: string;
  seeds?: string[];
  keyword?: string;
  keywords?: string[];
  padding?: string;
  randomSeeds?: number;
  palindrome?: boolean;
  onProgress?: (totalSeedsSearched: number, matchingSeeds: number, elapsedMs: number, resultCount?: number) => void;
  onResult?: (seed: string, score: number) => void;
  [key: string]: unknown;
}

/** One search result (seed + score). */
export interface SearchResultInfo {
  seed: string;
  score: number;
  [key: string]: unknown;
}

/** Seed analysis result (antes, deck, etc.). */
export interface SeedAnalysisInfo {
  seed?: string;
  error?: string;
  erraticDeckComposition?: string[];
  antes?: Array<{ drawOrder?: string; [key: string]: unknown }>;
  [key: string]: unknown;
}

/** Result of processBlock (one block). */
export interface BlockSearchResult {
  blockId: number;
  seedsSearched: number;
  seedsFound: number;
  seeds: Array<{ seed: string; score: number }>;
}

/** API returned by loadMotely (wrapper over native addon). */
export interface MotelyNodeApi {
  getCapabilities(): Promise<CapabilitiesInfo>;
  analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo>;
  validateJaml(jamlContent: string): Promise<{ valid: boolean; error?: string | null; name?: string | null; deck?: string | null; stake?: string | null }>;
  startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<SearchResultInfo[]>;
  processBlock(jamlContent: string, blockId: number): Promise<BlockSearchResult>;
  dispose(): void;
}

/**
 * Load Motely for Node.js from the packaged native addon.
 */
export function loadMotely(): Promise<MotelyNodeApi>;

/** Default export is the wrapped API (same as loadMotely() then await). */
declare const api: MotelyNodeApi;
export default api;
