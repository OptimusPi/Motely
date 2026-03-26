export type SeedSearchRow = {
  seed: string;
  score: number;
  tallies: number[];
};

export type SearchRequest = {
  jaml: string;
  threads: number;
  batchCharCount?: number;
  startBatch?: number;
  endBatch?: number;
};

export type SearchResponse = {
  ok: boolean;
  mode: "thin-client-placeholder";
  message: string;
  shouldLabels: string[];
  results: SeedSearchRow[];
  elapsedMs: number;
};
