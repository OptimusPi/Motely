"use client";

export { default as bootsharp } from "motely-wasm";
export * as Motely from "motely-wasm";

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
  MOTELY_BIN_PATH,
  type MotelyRuntimeStatus,
} from "./lib/motely/runtime.js";
