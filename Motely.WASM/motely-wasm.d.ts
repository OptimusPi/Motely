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

export interface MotelyWasmApi {
  /** Analyze a single seed. Returns JSON string. Use await (browser WASM requires async). */
  AnalyzeSeed(
    seed: string,
    deck: string,
    stake: string,
    minAnte: number,
    maxAnte: number,
    optionsJson: string
  ): Promise<string>;

  /**
   * Search for seeds. Progress and final result are pushed via globalThis.MotelyWasmOnProgress
   * and globalThis.MotelyWasmOnComplete if set. Also returns final result JSON when the promise resolves.
   */
  SearchSeeds(
    jamlFilterJson: string,
    seedList: string | null,
    threadCount: number
  ): Promise<string>;

  ValidateJaml(jamlString: string): Promise<string>;

  CancelSearch(): Promise<void>;
  IsSearchRunning(): Promise<boolean>;
  GetLastSearchResult(): Promise<string>;
  GetSearchProgress(): Promise<string>;
  GetProcessorCount(): Promise<number>;
  GetVersion(): Promise<string>;
}

/** Progress JSON (from MotelyWasmOnProgress). */
export interface MotelyWasmProgress {
  searchedCount: number;
  foundCount: number;
  status: string;
  percentComplete: number;
  seedsPerSecond: number;
  threadCount: number;
}

export function getDistPath(): string;
