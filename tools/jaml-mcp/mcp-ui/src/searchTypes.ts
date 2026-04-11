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
