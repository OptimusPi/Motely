"use client";

import "./ui/jimbo.css";

export {
  JamlCardRenderer,
  type JamlCardRendererProps,
} from "./render/CanvasRenderer.js";

export {
  JamlGameCard,
  JamlVoucher,
  JamlTag,
  JamlBoss,
  resolveAnalyzerShopItem,
  type JamlGameCardProps,
  type AnalyzerShopItem,
  type AnalyzerResolvedItem,
} from "./components/GameCard.js";

export {
  JamlyzerView,
  type JamlyzerViewProps,
} from "./components/JamlyzerView.js";

export {
  JimboBalatroFooter,
  type JimboBalatroFooterProps,
} from "./components/JimboBalatroFooter.js";

export {
  JamlyzerBulk,
  type JamlyzerBulkProps,
} from "./components/JamlyzerBulk.js";

export {
  parseJamlClauses,
  type ParsedJamlClause,
  type JamlClauseKind,
  type JamlItemType,
  type ParsedJamlFilters,
  matchClauseToItem,
  matchClauseToAnte,
  matchMotelyItemToClause,
} from "./lib/jaml/parseClauses.js";

export {
  DeckSprite,
  DECK_SPRITE_POS,
  STAKE_SPRITE_POS,
  type DeckSpriteProps,
} from "./components/DeckSprite.js";

export { StandardCard } from "./components/StandardCard.js";
export {
  CardSuit,
  CardRank,
  CardEnhancement,
  CardSeal,
  CardEdition,
} from "./components/cardEnums.js";

export {
  JamlMapEditor,
  CategoryMenu,
  MysterySlot,
  JokerPicker,
  CategoryPicker,
  type JamlMapEditorProps,
  type MysterySlotProps,
  type SlotSelection,
  type SlotCategory,
  type JokerPickerProps,
  type JokerRarity,
  type CategoryPickerConfig,
  type CategoryPickerProps,
  VOUCHER_PICKER_CONFIG,
  TAG_PICKER_CONFIG,
  BOSS_PICKER_CONFIG,
  TAROT_PICKER_CONFIG,
  PLANET_PICKER_CONFIG,
  SPECTRAL_PICKER_CONFIG,
  PACK_PICKER_CONFIG,
} from "./components/jamlMap/index.js";

export {
  JamlIde,
  type JamlIdeProps,
  type JamlIdeSearchResult,
  type JamlVisualFilter,
  type JamlVisualClause,
  type JamlZone,
} from "./components/JamlIde.js";
export { JamlIdeVisual, type JamlIdeVisualProps } from "./components/JamlIdeVisual.js";
export { JamlIdeToolbar, type JamlIdeToolbarProps, type JamlIdeMode } from "./components/JamlIdeToolbar.js";
export { JamlMapPreview, type JamlMapPreviewProps } from "./components/JamlMapPreview.js";

export * from "./ui.js";
// motely.js is NOT re-exported here on purpose. This entry is a client boundary
// ("use client" above), and re-exporting the pure decoders through it hands
// server callers a client-marked copy — decodeMotelyItemName then dies with
// "is on the client" even though dist/motely.js itself is clean. Server callers
// import from "jaml-ui/motely", which is what that subpath export is for.

// ── json-render v2 — zero-dep JSON-to-React engine ──
export * from "./json-render/index.js";
