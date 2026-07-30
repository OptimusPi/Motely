export { render, renderList, defineCatalog } from "./engine.js";
export type { JsonNode, ComponentProps, Registry } from "./engine.js";

export { balatroCatalog } from "./catalog.js";
export type { BalatroCatalog, BalatroComponentName, CatalogProps } from "./catalog.js";

export { balatroRegistry } from "./registry.js";
export type { BalatroRegistry } from "./registry.js";

export {
  Panel, Stack, Grid, Text, Spacer, Divider, Badge,
} from "./components/layout.js";
export type {
  PanelProps, StackProps, GridProps, TextProps, SpacerProps, DividerProps, BadgeProps,
} from "./components/layout.js";

export {
  SearchStats, ErrorBanner, LoadingPulse,
  SeedCard, SeedList, JokerBadge, EditionBadge,
} from "./components/domain.js";
export type {
  SearchStatsProps, ErrorBannerProps, LoadingPulseProps,
  SeedCardProps, SeedListProps, JokerBadgeProps, EditionBadgeProps,
} from "./components/domain.js";

export { JamlGameCard } from "./components/game.js";
export type { JamlGameCardProps } from "./components/game.js";

export { JammyMascot, JammyOrbitalMenu } from "./components/mascot.js";
export type {
  JammyMascotProps,
  JammyOrbitalMenuProps,
  JammyOrbitalMenuItem,
} from "./components/mascot.js";

export {
  JokerCard, SynergyCard, BossBlindCard, DeckCard, StakeCard, StrategyAdvisor,
} from "./components/reference.js";
export type {
  JokerCardProps, SynergyCardProps, BossBlindCardProps, DeckCardProps, StakeCardProps, StrategyAdvisorProps,
} from "./components/reference.js";

export { buildSearchSpec } from "./builders/search.js";
export type { SearchResult, SearchParams } from "./builders/search.js";

export { buildEncyclopediaSpec } from "./builders/encyclopedia.js";
export type { EncyclopediaParams } from "./builders/encyclopedia.js";

export { buildAnalyzerSpec } from "./builders/analyzer.js";
export type { AnalyzerResult } from "./builders/analyzer.js";

// ── Knowledge Base ──
export { JOKERS, getJoker, getJokersByCategory, getSynergies } from "./knowledge/jokers.js";
export type { JokerInfo, JokerRarity, JokerCategory } from "./knowledge/jokers.js";

export { SYNERGIES, findSynergies, findSynergiesByTag, getRecommendedSynergies } from "./knowledge/synergies.js";
export type { SynergyInfo } from "./knowledge/synergies.js";

export { DECKS, STAKES, getDeck, getStake } from "./knowledge/decks.js";
export type { DeckInfo, StakeInfo } from "./knowledge/decks.js";

export { BOSSES, FINISHERS, getBoss, getBossesByCategory, getAllBosses } from "./knowledge/bosses.js";
export type { BossInfo, BossCategory } from "./knowledge/bosses.js";
