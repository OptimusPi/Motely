import type { CapabilitiesInfo, LoadMotelyOptions, MotelyNodeApi, SearchOptions, SearchResultInfo, SeedAnalysisInfo } from './index.js';
export declare function getServerApi(options?: LoadMotelyOptions): Promise<MotelyNodeApi>;
export declare function disposeServerApi(): Promise<void>;
export declare function analyzeSeedServer(seed: string, deck: string, stake: string, options?: LoadMotelyOptions): Promise<SeedAnalysisInfo>;
export declare function startJamlSearchServer(jamlContent: string, options?: SearchOptions, loadOptions?: LoadMotelyOptions): Promise<SearchResultInfo[]>;
/** Search all palindrome seeds (e.g. ABCDDCBA, 12344321). */
export declare function startPalindromeSearchServer(jamlContent: string, options?: Omit<SearchOptions, 'palindrome' | 'seeds' | 'specificSeed' | 'keyword' | 'keywords' | 'randomSeeds'>, loadOptions?: LoadMotelyOptions): Promise<SearchResultInfo[]>;
/** Search all 8-char padded variations containing the keyword. */
export declare function startKeywordSearchServer(jamlContent: string, keyword: string, padding?: string, options?: Omit<SearchOptions, 'keyword' | 'keywords' | 'padding' | 'seeds' | 'specificSeed' | 'randomSeeds' | 'palindrome'>, loadOptions?: LoadMotelyOptions): Promise<SearchResultInfo[]>;
/** Search all 8-char padded variations for every keyword in the list. */
export declare function startKeywordsSearchServer(jamlContent: string, keywords: string[], padding?: string, options?: Omit<SearchOptions, 'keyword' | 'keywords' | 'padding' | 'seeds' | 'specificSeed' | 'randomSeeds' | 'palindrome'>, loadOptions?: LoadMotelyOptions): Promise<SearchResultInfo[]>;
/** Search an explicit list of seeds. */
export declare function startSeedListSearchServer(jamlContent: string, seeds: string[], options?: Omit<SearchOptions, 'seeds' | 'specificSeed' | 'keyword' | 'keywords' | 'randomSeeds' | 'palindrome'>, loadOptions?: LoadMotelyOptions): Promise<SearchResultInfo[]>;
/** Verify a single specific seed against the filter. */
export declare function verifySeedServer(jamlContent: string, seed: string, options?: Omit<SearchOptions, 'specificSeed' | 'seeds' | 'keyword' | 'keywords' | 'randomSeeds' | 'palindrome'>, loadOptions?: LoadMotelyOptions): Promise<SearchResultInfo[]>;
/** Search N random seeds. */
export declare function startRandomSearchServer(jamlContent: string, count: number, options?: Omit<SearchOptions, 'randomSeeds' | 'seeds' | 'specificSeed' | 'keyword' | 'keywords' | 'palindrome'>, loadOptions?: LoadMotelyOptions): Promise<SearchResultInfo[]>;
export declare function getServerCapabilities(options?: LoadMotelyOptions): Promise<CapabilitiesInfo>;
export type { CapabilitiesInfo, LoadMotelyOptions, MotelyNodeApi, SearchOptions, SearchResultInfo, SeedAnalysisInfo, } from './index.js';
