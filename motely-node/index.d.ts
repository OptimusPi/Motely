// motely-node TypeScript declarations
// Native AOT bindings for MotelyJAML seed search engine

export interface SeedAnalysisDto {
  seed: string;
  deck: string;
  stake: string;
  erraticDeckComposition: string[];
  antes: AnteAnalysisDto[];
}

export interface AnteAnalysisDto {
  ante: number;
  boss: string;
  voucher: string;
  smallBlindTag: string;
  bigBlindTag: string;
  drawOrder: string;
  shopQueue: ShopItemDto[];
  packs: PackDto[];
}

export interface ShopItemDto {
  id: string;
  name: string;
}

export interface PackDto {
  type: string;
  items: string[];
}

export interface BlockSearchResultDto {
  blockId: number;
  seedsFound: number;
  highestScore: number;
  seeds: string[];
}

export function getVersion(): string;
export function isSimdEnabled(): boolean;
export function getProcessorCount(): number;

export function analyzeSeed(seed: string, deck: string, stake: string): SeedAnalysisDto;

export function validateJaml(jamlContent: string): boolean;
export function validateJamlWithError(jamlContent: string): string;

export function runKeywordSearchAsync(
  jamlContent: string,
  keyword: string,
  padding?: string
): Promise<BlockSearchResultDto>;

export function runKeywordsSearchAsync(
  jamlContent: string,
  keywords: string[],
  padding?: string
): Promise<BlockSearchResultDto>;

export function runRandomSearchAsync(
  jamlContent: string,
  count: number
): Promise<BlockSearchResultDto>;

export function runPalindromeSearchAsync(
  jamlContent: string
): Promise<BlockSearchResultDto>;

export function runListSearchAsync(
  jamlContent: string,
  seeds: string[]
): Promise<BlockSearchResultDto>;

export function runSequentialRangeAsync(
  jamlContent: string,
  startBlockId: number,
  endBlockId: number
): Promise<BlockSearchResultDto>;

export function processBlockAsync(
  jamlContent: string,
  blockId: number
): Promise<BlockSearchResultDto>;
