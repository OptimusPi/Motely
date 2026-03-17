// motely-node — TypeScript declarations for the Node.js native addon API

export interface CapabilitiesInfo {
  simd: boolean;
  threads: boolean;
  availableThreadCount: number;
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
  tallies?: string[] | null;
}

export interface BlockSeedResult {
  seed: string;
  score: number;
}

export interface BlockSearchResult {
  blockId: number;
  seedsSearched: number;
  seedsFound: number;
  seeds: BlockSeedResult[];
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
  randomSeeds?: number;
  cutoff?: string;
  startBatch?: number;
  endBatch?: number;
  specificSeed?: string;
  seeds?: string[];
  keyword?: string;
  keywords?: string[];
  padding?: string;
  palindrome?: boolean;
  onProgress?: (seedsSearched: number, matchingSeeds: number, elapsedMs: number, resultCount: number) => void;
  onResult?: (seed: string, score: number) => void;
}

export interface MotelyNodeApi {
  getCapabilities(): CapabilitiesInfo;
  analyzeSeed(seed: string, deck: string, stake: string): SeedAnalysisInfo;
  validateJaml(jamlContent: string): ValidateResult;
  startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<SearchResultInfo[]>;
  processBlock(jamlContent: string, blockId: number): Promise<BlockSearchResult>;
  dispose(): void;
}

export function loadMotely(): Promise<MotelyNodeApi>;

declare const api: {
  getCapabilities(): Promise<CapabilitiesInfo>;
  analyzeSeed(seed: string, deck: string, stake: string): Promise<SeedAnalysisInfo>;
  validateJaml(jamlContent: string): Promise<ValidateResult>;
  startJamlSearch(jamlContent: string, options?: SearchOptions): Promise<SearchResultInfo[]>;
  processBlock(jamlContent: string, blockId: number): Promise<BlockSearchResult>;
  dispose(): void;
};

export default api;
