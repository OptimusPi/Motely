/**
 * Type definitions for Motely WASM JS exports.
 * After loading main.js from the app-bundle, you get an object with these methods.
 *
 * Callback-based search (MS [JSImport] pattern): set these on globalThis before calling SearchSeeds
 * to receive progress, each result, and completion pushed from C#:
 *   globalThis.MotelyWasmOnProgress = (progressJson: string) => void;
 *   globalThis.MotelyWasmOnResult = (seed: string, score: number, talliesStr: string) => void; // talliesStr = comma-separated ints
 *   globalThis.MotelyWasmOnComplete = (resultJson: string) => void;
 */

// ============================================================================
// WASM API Interface
// ============================================================================

export interface MotelyWasmApi {
  /** Analyze a single seed. Returns JSON string (SeedAnalysisResult or ErrorResult). */
  AnalyzeSeed(
    seed: string,
    deck: string,
    stake: string,
    minAnte: number,
    maxAnte: number,
    optionsJson: string
  ): Promise<string>;

  /**
   * Search for seeds matching a JAML filter.
   * Progress and results are pushed via globalThis callbacks.
   * Returns final summary JSON (SearchResponse or ErrorResult).
   */
  SearchSeeds(
    jamlFilterJson: string,
    seedList: string | null,
    threadCount: number,
    maxResults?: number
  ): Promise<string>;

  /** Validate a JAML filter string without running a search. */
  ValidateJaml(jamlString: string): Promise<string>;

  /** Cancel an in-progress search. */
  CancelSearch(): Promise<void>;

  /** Check if a search is currently running. */
  IsSearchRunning(): Promise<boolean>;

  /** Get the last search result JSON (cleared after retrieval). */
  GetLastSearchResult(): Promise<string>;

  /** Get current search progress JSON. */
  GetSearchProgress(): Promise<string>;

  /** Get processor count for thread hints. */
  GetProcessorCount(): Promise<number>;

  /** Get version info. */
  GetVersion(): Promise<string>;
}

// ============================================================================
// Seed Analysis DTOs (returned by AnalyzeSeed)
// ============================================================================

/** Full seed analysis result. */
export interface SeedAnalysisResult {
  seed: string;
  deck: string;
  stake: string;
  erraticDeckComposition: string[];
  twos: number;
  error?: string;
  antes: AnteAnalysis[];
}

/** Analysis data for a single ante. */
export interface AnteAnalysis {
  ante: number;
  boss: string;
  voucher: string;
  smallBlindTag: string;
  bigBlindTag: string;
  drawOrder: string;
  shopQueue: ShopItem[];
  packs: Pack[];
}

/** Shop item (joker, tarot, planet, etc.). */
export interface ShopItem {
  id: string;
  name: string;
}

/** Booster pack with its contents. */
export interface Pack {
  type: string;
  items: string[];
}

// ============================================================================
// Search DTOs (returned by SearchSeeds, via callbacks)
// ============================================================================

/** Final search response (returned by SearchSeeds promise and MotelyWasmOnComplete). */
export interface SearchResponse {
  results: SearchHit[];
  totalSearched: number;
  foundCount: number;
  cancelled: boolean;
}

/** Individual search hit (pushed via MotelyWasmOnResult as individual params). */
export interface SearchHit {
  seed: string;
  score: number;
  tallies?: number[];
}

/** Progress update (pushed via MotelyWasmOnProgress). */
export interface SearchProgress {
  searchedCount: number;
  foundCount: number;
  status: string;
  percentComplete: number;
  seedsPerSecond: number;
  threadCount: number;
}

// ============================================================================
// Validation DTOs (returned by ValidateJaml)
// ============================================================================

/** JAML validation result. */
export interface ValidateResult {
  valid: boolean;
  error?: string;
  name?: string;
  deck?: string;
  stake?: string;
}

// ============================================================================
// Version/Error DTOs
// ============================================================================

/** Version info (returned by GetVersion). */
export interface VersionInfo {
  version: string;
  runtime: string;
  features: string[];
}

/** Error response (returned when operations fail). */
export interface ErrorResult {
  error: string;
}

// ============================================================================
// Legacy Aliases (for backward compatibility)
// ============================================================================

/** @deprecated Use SearchProgress instead */
export type MotelyWasmProgress = SearchProgress;

// ============================================================================
// NPM Package Exports
// ============================================================================

/** Get the path to the dist folder containing WASM files. Copy to your public dir. */
export function getDistPath(): string;

/** Get the path to the _framework folder containing the WASM runtime. */
export function getFrameworkPath(): string;

// NOTE: loadMotely() was removed because dynamic imports break bundlers (Webpack/Turbopack).
// Load the WASM module directly in your browser code:
//   const { dotnet } = await import('/motely-wasm/_framework/dotnet.js');
//   const { getAssemblyExports, getConfig } = await dotnet.create();
//   const api = (await getAssemblyExports(getConfig().mainAssemblyName)).Motely.WASM.MotelyWasm;
