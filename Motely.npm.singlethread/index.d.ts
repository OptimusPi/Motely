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

export interface SearchOptions {
  threadCount?: number;
  batchCharCount?: number;
  startBatch?: number;
  endBatch?: number;
  cutoff?: string;
  specificSeed?: string;
  randomSeeds?: number;
  palindrome?: boolean;
  onProgress?: (totalSeedsSearched: number, matchingSeeds: number, elapsedMs: number, resultCount: number) => void;
  onResult?: (seed: string, score: number) => void;
}

export interface MotelyWasmApi {
  getVersion(): VersionInfo;
  getCapabilities(): CapabilitiesInfo;
  isSimdEnabled(): boolean;
  isThreadingEnabled(): boolean;
  getProcessorCount(): number;
  analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo>;
  validateJaml(jamlContent: string): Promise<ValidateResult>;
  startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<SearchStatusInfo>;
  stopSearch(): void;
  disposeSearch(): Promise<void>;
}

export interface LoadMotelyOptions {
  baseUrl?: string;
}

export declare function loadMotely(options?: LoadMotelyOptions): Promise<MotelyWasmApi>;
