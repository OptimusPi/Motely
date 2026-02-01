/**
 * Node/build-script entry (require('motely-wasm')).
 * Runtime: only getDistPath and getFrameworkPath.
 * For loadMotely and browser API use ESM: import { loadMotely } from 'motely-wasm'
 */
export function getDistPath(): string;
export function getFrameworkPath(): string;

export type {
  MotelyWasmApi,
  SeedAnalysisResult,
  AnteAnalysis,
  ShopItem,
  Pack,
  SearchResponse,
  SearchHit,
  SearchProgress,
  ValidateResult,
  VersionInfo,
  ErrorResult,
} from "./motely-wasm";

export { initDuckDbWasmResults } from "./dist/loader";
export type {
  DuckDbWasmResultsHandle,
  DuckDbWasmResultsOptions,
} from "./dist/loader";
