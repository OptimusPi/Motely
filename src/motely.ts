"use client";

export { default as bootsharp, Motely } from "motely-wasm";

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
  warmMotelyItemCache,
  motelyItemCacheSize,
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
  useMotelyRuntime,
  type UseMotelyRuntimeState,
  type MotelyRuntimeStatus,
} from "./hooks/useMotelyRuntime.js";
export {
  MotelyProvider,
  useMotelyContext,
  type MotelyContextValue,
} from "./providers/MotelyProvider.js";
