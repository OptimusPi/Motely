export interface SearchResult {
  seed: string;
  score: number;
  tally: number[] | Int32Array;
}

export interface SearchResponse {
  status: string;
  seedsSearched: string;
  matchesFound: string;
  totalMatches?: string;
  resultsShown?: string;
  results: SearchResult[];
}

export interface AnteAnalysis {
  ante: number;
  boss: string;
  voucher: string;
  smallBlindTag: string;
  bigBlindTag: string;
  drawOrder?: string;
  shopQueue: string[];
  packs: { type: string; items: string[] }[];
}

export interface SeedAnalysis {
  seed?: string;
  deck?: string;
  error?: string;
  erraticDeckComposition?: string;
  antes: AnteAnalysis[];
}
