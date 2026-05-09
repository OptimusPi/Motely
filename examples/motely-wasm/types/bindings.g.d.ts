import type { EventBroadcaster, EventSubscriber } from "./event";

export namespace Bootsharp.FileSystem {
    export interface PickOptions {
        id?: string;
        mode: Bootsharp.FileSystem.PermissionMode;
        startIn?: string;
        under?: string;
    }
    export enum PermissionMode {
        Read,
        ReadWrite
    }
    export interface IFileWatcher {
        handleFileChanges(changes: Array<Bootsharp.FileSystem.Change>): Promise<void>;
    }
    export interface MountOptions {
        mode: Bootsharp.FileSystem.PermissionMode;
        ignore?: Array<string>;
    }
    export interface IFileSystem {
        createDirectory(uri: string): Promise<void>;
        removeDirectory(uri: string): Promise<void>;
        moveDirectory(fromUri: string, toUri: string): Promise<void>;
        getFileInfo(uri: string): Promise<Bootsharp.FileSystem.FileInfo>;
        readFile(uri: string): Promise<Uint8Array>;
        writeFile(uri: string, content: Uint8Array): Promise<void>;
        deleteFile(uri: string): Promise<void>;
        moveFile(fromUri: string, toUri: string): Promise<void>;
    }
    export interface Change {
        type: Bootsharp.FileSystem.ChangeType;
        entry: Bootsharp.FileSystem.Entry;
        fromUri?: string;
        added: boolean;
        removed: boolean;
        modified: boolean;
        moved: boolean;
        file: boolean;
        directory: boolean;
    }
    export enum ChangeType {
        Added,
        Removed,
        Moved,
        Modified
    }
    export interface Entry {
        uri: string;
        type: Bootsharp.FileSystem.EntryType;
    }
    export enum EntryType {
        File,
        Directory
    }
    export interface FileInfo {
        type: string;
        bytesCount: number;
        lastModified: Date;
    }
}
export namespace Motely {
    export interface MotelyItemLayout {
        itemTypeMask: number;
        standardcardRankMask: number;
        standardcardSuitOffset: number;
        standardcardSuitMask: number;
        itemTypeCategoryOffset: number;
        itemTypeCategoryMask: number;
        jokerRarityOffset: number;
        jokerRarityMask: number;
        itemSealOffset: number;
        itemSealMask: number;
        itemEnhancementOffset: number;
        itemEnhancementMask: number;
        itemEditionOffset: number;
        itemEditionMask: number;
        perishableStickerOffset: number;
        eternalStickerOffset: number;
        rentalStickerOffset: number;
    }
    export interface JamlValidationResult {
        valid: boolean;
        message?: string;
        path?: string;
        line: number;
        column: number;
    }
    export interface JamlMetaResult {
        antes: Int32Array;
        itemTypes: Array<string>;
        mustCount: number;
        shouldCount: number;
        mustNotCount: number;
        deck: string;
        stake: string;
    }
    export enum MotelyDeck {
        Red,
        Blue,
        Yellow,
        Green,
        Black,
        Magic,
        Nebula,
        Ghost,
        Abandoned,
        Checkered,
        Zodiac,
        Painted,
        Anaglyph,
        Plasma,
        Erratic
    }
    export enum MotelyStake {
        White,
        Red,
        Green,
        Black,
        Blue,
        Purple,
        Orange,
        Gold
    }
    export interface IMotelyWasmSearch {
        getSnapshot(): Motely.MotelyWasmSearchSnapshot;
        cancel(): void;
        waitForCompletion(): Promise<Motely.MotelyWasmSearchCompletion>;
    }
    export interface MotelyWasmSearchBatchResult {
        completion: Motely.MotelyWasmSearchCompletion;
        results: Array<Motely.MotelyWasmSearchResult>;
    }
    export interface MotelyWasmSearchCompletion {
        state: Motely.MotelyWasmSearchState;
        totalSeedsSearched: bigint;
        matchingSeeds: bigint;
        error?: string;
    }
    export enum MotelyWasmSearchState {
        Running,
        Completed,
        Cancelled,
        Faulted
    }
    export interface MotelyWasmSearchResult {
        seed: string;
        score: number;
        tallyColumns: Int32Array;
    }
    export interface MotelyWasmSearchSnapshot {
        elapsedMs: bigint;
        totalSeedsSearched: bigint;
        matchingSeeds: bigint;
        filteredSeeds: bigint;
        isCompleted: boolean;
        isSequentialBatchSearch: boolean;
        batchIndex: bigint;
        completedBatchCount: bigint;
    }
}
export namespace Motely.Analysis {
    export interface MotelyJamlyzerResult {
        error?: string;
        seeds: Array<Motely.Analysis.MotelyJamlyzerSeedResult>;
        deck?: Motely.MotelyDeck;
        stake?: Motely.MotelyStake;
        tallyLabels?: Array<string>;
        totalSeedsSearched: bigint;
        matchingSeeds: bigint;
        completedBatchCount: bigint;
    }
    export interface MotelyJamlyzerSeedResult {
        seed: string;
        score: number;
        tallies: Int32Array;
        analysis?: Motely.Analysis.SeedAnalysisDto;
    }
    export interface SeedAnalysisDto {
        seed: string;
        deck: string;
        stake: string;
        erraticDeckComposition: Array<string>;
        error?: string;
        antes: Array<Motely.Analysis.AnteAnalysisDto>;
    }
    export interface AnteAnalysisDto {
        ante: number;
        boss: string;
        voucher: string;
        smallBlindTag: string;
        bigBlindTag: string;
        drawOrder: string;
        shopQueue: Array<Motely.Analysis.ShopItemDto>;
        packs: Array<Motely.Analysis.PackDto>;
    }
    export interface ShopItemDto {
        id: string;
        name: string;
        value: number;
        matched: boolean;
    }
    export interface PackDto {
        type: string;
        items: Array<Motely.Analysis.ShopItemDto>;
    }
}
export namespace Motely.Filters {
    export enum JamlAesthetic {
        Palindrome,
        Psychosis,
        Gross,
        Nsfw,
        Funny,
        Balatro
    }
}

export namespace Bootsharp.FileSystem.FileMounter {
    export let pickRoot: (options: Bootsharp.FileSystem.PickOptions | undefined) => Promise<string | null>;
    export let mount: (root: string, watcher: Bootsharp.FileSystem.IFileWatcher, options: Bootsharp.FileSystem.MountOptions | undefined) => Promise<Bootsharp.FileSystem.IFileSystem>;
    export let unmount: (root: string) => Promise<void>;
}
export namespace Motely.MotelyWasm {
    export function getVersion(): string;
    export function getItemLayout(): Motely.MotelyItemLayout;
    export function getJamlSchema(): string;
    export function validateJaml(jaml: string): string;
    export function validateJamlStructured(jaml: string): Motely.JamlValidationResult;
    export function getJamlMeta(jaml: string): Motely.JamlMetaResult;
    export function explainJamlPerformance(jaml: string): string;
    export function getTallyLabels(jaml: string): Array<string>;
    export function analyzeJamlSeeds(jaml: string, seeds: Array<string>): Motely.Analysis.MotelyJamlyzerResult;
    export function startRandomSearch(jaml: string, randomSeedCount: number): Motely.IMotelyWasmSearch;
    export function startAestheticSearch(jaml: string, aesthetic: Motely.Filters.JamlAesthetic): Motely.IMotelyWasmSearch;
    export function startSequentialSearch(jaml: string, batchCharCount: number, startBatch: bigint, endBatch: bigint): Motely.IMotelyWasmSearch;
    export function runSequentialSearchBatch(jaml: string, batchCharCount: number, startBatch: bigint, endBatch: bigint, maxResults: number): Promise<Motely.MotelyWasmSearchBatchResult>;
    export function startSeedListSearch(jaml: string, seeds: Array<string>): Motely.IMotelyWasmSearch;
    export function startKeywordSearch(jaml: string, keywordsCsv: string, paddingChars: string): Motely.IMotelyWasmSearch;
    export function mountJamlLibrary(): Promise<string | null>;
    export function unmountJamlLibrary(rootId: string): Promise<void>;
    export function getJamlLibraryFiles(rootId: string): Array<string>;
    export function loadLibraryFile(rootId: string, uri: string): Promise<string>;
    export function saveLibraryFile(rootId: string, uri: string, content: string): Promise<void>;
}
export namespace Motely.MotelyWasmEvents {
    export let notifyProgress: (seedsSearched: bigint, matchingSeeds: bigint) => void;
    export let notifyResult: (seed: string, score: number, tallyColumns: Int32Array) => void;
    export let notifyComplete: (status: string, totalSeedsSearched: bigint, matchingSeeds: bigint) => void;
    export let notifyJamlLibraryChanged: (rootId: string, fileUris: Array<string>) => void;
}
