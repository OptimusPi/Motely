/** Addon exports from C# [JSExport]. All async methods return JSON strings. */
declare const addon: {
  GetVersionAsync(): Promise<string>;
  GetCapabilitiesAsync(): Promise<string>;
  AnalyzeSeedAsync(seed: string, deck: string, stake: string): Promise<string>;
  ValidateJamlAsync(jamlContent: string): Promise<string>;
  StartJamlSearch(jamlContent: string, optionsJson: string): Promise<string>;
  GetSearchStatus(): Promise<string>;
  ProcessBlockAsync(jamlContent: string, blockId: number): Promise<string>;
  StopSearch(): void;
  DisposeSearch(): Promise<void>;
};

/** Options for loadMotely (addon path, directory, poll interval). */
export interface LoadMotelyOptions {
  addonPath?: string;
  addonDirectory?: string;
  frameworkPath?: string;
  pollIntervalMs?: number;
}

/** Runtime capabilities (SIMD, threads, etc.). */
export interface CapabilitiesInfo {
  availableThreadCount?: number;
  [key: string]: unknown;
}

/** Options for startJamlSearch. */
export interface SearchOptions {
  threadCount?: number;
  batchCharCount?: number;
  cutoff?: string | number;
  specificSeed?: string;
  seeds?: string[];
  keyword?: string;
  keywords?: string[];
  padding?: string;
  randomSeeds?: number;
  palindrome?: boolean;
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

/** API returned by loadMotely (wrapper over addon). */
export interface MotelyNodeApi {
  getCapabilities(): Promise<CapabilitiesInfo>;
  analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo>;
  startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<SearchResultInfo[]>;
  dispose(): void;
}

/**
 * Load Motely for Node.js. Returns a Promise of the API object.
 * Options are optional; addon is loaded from package bin path when omitted.
 */
export function loadMotely(options?: LoadMotelyOptions): Promise<MotelyNodeApi>;

export default addon;
