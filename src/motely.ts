"use client";

// motely-wasm@23 ships a single root entry: the bootsharp default export plus the
// flattened namespaces (MotelyJaml / MotelySearch / MotelyJamlyzer / MotelyUtilities
// / Jimmolate), enums, and types. The old `Program` aggregate is gone; re-export the
// real v23 surface so `jaml-ui/motely` consumers reach the engine directly.
export {
    default as bootsharp,
    MotelyWasm,
    MotelyJaml,
    MotelyJummy,
    MotelyUtilities,
    MotelyJamlyzer,
    MotelySearch,
    Jimmolate,
    MotelyDeck,
    MotelyStake,
    MotelyBossBlind,
    MotelyVoucher,
    MotelyTag,
    MotelyBoosterPack,
    MotelyItemEdition,
    JamlAesthetic,
} from "motely-wasm";
export type {
    JamlConfig,
    IJamlClause,
    JamlSearchPlan,
    MotelyProgress,
    MotelyScoredSeedResult,
    MotelyItem,
    MotelyJamlyzerSeedResult,
    MotelyJamlyzerAnteResult,
} from "motely-wasm";

export {
  decodeMotelyItem,
  decodeMotelyItemToJamlCard,
  motelyItemTypeName,
  motelyItemCategory,
  motelyItemDisplayName,
  motelyItemRenderCategory,
  motelyItemEditionName,
  motelyItemSealName,
  motelyItemEnhancementName,
  motelyStandardcardRankName,
  motelyStandardcardSuitName,
  decodeMotelyItemName,
  resolveMotelyItemType,
  type DecodedMotelyItem,
  type MotelyItemInput,
  type MotelyJamlCard,
  type MotelyRenderableCategory,
  type MotelyRuntimeItem,
} from "./decode/motelyItemDecoder.js";

export {
  motelyItemToSprite,
  getMotelySpriteByName,
  type MotelySpriteData,
} from "./decode/motelySprite.js";

export {
  motelyBossDisplayName,
  motelyBossDisplayNameFromKey,
  motelyBoosterPackDisplayName,
  motelyBoosterPackDisplayNameFromKey,
  motelyItemDisplayNameFromKey,
  motelyItemDisplayNameFromValue,
  motelyTagDisplayName,
  motelyTagDisplayNameFromKey,
  motelyVoucherDisplayName,
  motelyVoucherDisplayNameFromKey,
} from "./motelyDisplay.js";

export {
  useJamlLibrary,
  type JamlLibraryStatus,
  type UseJamlLibraryState,
} from "./hooks/useJamlLibrary.js";
export {
  ensureMotelyReady,
  parseJaml,
  tallyLabelsFor,
  runSearch,
  aestheticSeeds,
  analyzeSeeds,
  setJimmolateProbe,
  clearJimmolateProbe,
  enableJimmolate,
  isFileSystemReady,
  getFileSystemError,
  MOTELY_BIN_PATH,
  type MotelyRuntimeStatus,
  type EngineSearchMode,
  type RunSearchOptions,
} from "./lib/motely/runtime.js";
