import type { Registry } from "./engine.js";
import {
  Panel,
  Stack,
  Grid,
  Text,
  Spacer,
  Divider,
  Badge,
} from "./components/layout.js";
import {
  SearchStats,
  ErrorBanner,
  LoadingPulse,
  SeedCard,
  SeedList,
  JokerBadge,
  EditionBadge,
} from "./components/domain.js";
import { JamlGameCard } from "./components/game.js";
import { JammyMascot, JammyOrbitalMenu } from "./components/mascot.js";
import {
  JokerCard,
  SynergyCard,
  BossBlindCard,
  DeckCard,
  StakeCard,
  StrategyAdvisor,
} from "./components/reference.js";

/**
 * Balatro Component Registry
 *
 * Maps catalog component names to real React implementations.
 * This is the ONLY place where catalog names are bound to code.
 * Add new components here → they instantly work in json-render specs.
 */
export const balatroRegistry: Registry = {
  // ── Layout ──
  Panel,
  Stack,
  Grid,
  Text,
  Spacer,
  Divider,
  Badge,

  // ── Status ──
  SearchStats,
  ErrorBanner,
  LoadingPulse,

  // ── Results ──
  SeedCard,
  SeedList,
  JokerBadge,
  EditionBadge,

  // ── Game Cards ──
  JamlGameCard,

  // ── Mascot ──
  JammyMascot,
  JammyOrbitalMenu,

  // ── Encyclopedia ──
  JokerCard,
  SynergyCard,
  BossBlindCard,
  DeckCard,
  StakeCard,
  StrategyAdvisor,
};

/**
 * Type-safe registry helper.
 * Ensures all catalog names have a matching component at compile time.
 */
export type BalatroRegistry = typeof balatroRegistry;
