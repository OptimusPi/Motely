// motely-wasm is a single-file native-LLVM ESM module that runs in Node and in the
// browser alike, so this barrel stays isomorphic. A "use client" directive here would
// mark every re-export below as client-only — including the pure enum decoders — and
// server callers (the MCP JAMLyzer) would fail at import with a client-boundary error.
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
