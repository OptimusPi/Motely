// Motely WASM Package Entry Point
// Exported methods return marshalled objects (no JSON); shapes match .NET DTOs.

export interface VersionInfo {
  version: string;
  runtime: string;
  framework: string;
}

export interface CapabilitiesInfo {
  simd: boolean;
  threads: boolean;
  processorCount: number;
  runtime: string;
  version: string;
  timestamp: string;
  note?: string | null;
}

export interface SearchResultInfo {
  seed: string;
  score: number;
  tallies?: number[] | null;
}

export interface SearchStatusInfo {
  searchId: string;
  status: string;
  isRunning: boolean;
  progressPercent: number;
  totalSeedsSearched: number;
  matchingSeeds: number;
  resultCount: number;
  results: SearchResultInfo[];
  error?: string | null;
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
  twos: number;
  error?: string | null;
  antes: AnteAnalysisInfo[];
}

export interface MotelyWasmApi {
  GetVersion(): VersionInfo;
  GetCapabilities(): CapabilitiesInfo;
  IsSimdEnabled(): boolean;
  IsThreadingEnabled(): boolean;
  GetProcessorCount(): number;
  AnalyzeSeed(
    seed: string,
    deck: string,
    stake: string,
    ante: number,
    shop: number,
    config: string
  ): SeedAnalysisInfo;
  StartJamlSearch(
    jamlContent: string,
    deck?: string,
    stake?: string,
    threads?: number,
    batchSize?: number,
    startBatch?: number,
    endBatch?: number,
    cutoff?: number
  ): string;
  GetSearchStatus(searchId: string, limit?: number): SearchStatusInfo;
  StopSearch(searchId: string): void;
}

export interface LoadMotelyOptions {
  /** Base URL for _framework (e.g. "/_framework" or "https://cdn.example/assets"). Default "/_framework". */
  baseUrl?: string;
}

export async function loadMotely(options?: LoadMotelyOptions): Promise<MotelyWasmApi> {
  const base = (options?.baseUrl ?? "/_framework").replace(/\/$/, "") || "/_framework";
  const origin = typeof window !== "undefined" ? window.location.origin : "https://localhost";
  const url = base.startsWith("http") ? base : new URL(base, origin).href;
  const dotnetUrl = `${url}/dotnet.js`;
  const { dotnet } = await import(/* @vite-ignore */ dotnetUrl);
  const { getAssemblyExports, getConfig } = await dotnet.create();
  const config = getConfig();
  const exports = await getAssemblyExports(config.mainAssemblyName);
  return exports.Motely.WASM.MotelyWasm;
}
