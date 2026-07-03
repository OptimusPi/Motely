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

export * from "./ui.js";
export * from "./motely.js";

// ── json-render v2 — zero-dep JSON-to-React engine ──
export * from "./json-render/index.js";
