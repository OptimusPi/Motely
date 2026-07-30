import { defineCatalog } from "./engine";
import type { BadgeTone } from "./components/layout.js";

/**
 * Balatro Catalog — Component vocabulary for AI-generated UI.
 *
 * Type-safe at compile time. Zero runtime overhead.
 * The AI sees these names + descriptions + prop shapes.
 */
export const balatroCatalog = defineCatalog({
  // ── Layout ──
  Panel: {
    props: {} as {
      title?: string;
      subtitle?: string;
      variant?: "default" | "accent" | "muted";
      className?: string;
    },
    description:
      "A framed panel with optional title. Use for grouping related content.",
  },
  Stack: {
    props: {} as {
      gap?: number; // default 12
      align?: "start" | "center" | "end" | "stretch"; // default stretch
      className?: string;
    },
    description: "Vertical stack with configurable gap. The default layout primitive.",
  },
  Grid: {
    props: {} as {
      columns?: number; // 1-4, default 3
      gap?: number; // default 16
      className?: string;
    },
    description:
      "Grid layout for cards and results. Responsive: 1 col mobile, 2 tablet, 3+ desktop.",
  },
  Text: {
    props: {} as {
      body: string;
      variant?: "title" | "body" | "muted" | "accent" | "error";
      className?: string;
    },
    description: "Styled text. Use variant for semantic color, not manual color.",
  },
  Spacer: {
    props: {} as { size?: number },
    description: "Vertical spacer. Use instead of empty divs with margin.",
  },
  Divider: {
    props: {} as { className?: string },
    description: "Horizontal divider line.",
  },
  Badge: {
    props: {} as {
      label: string;
      tone?: "red" | "blue" | "green" | "orange" | "gold" | "purple" | "grey";
      className?: string;
    },
    description: "Small colored badge / pill.",
  },

  // ── Status & Feedback ──
  SearchStats: {
    props: {} as {
      status: "idle" | "running" | "completed" | "error";
      seedsSearched?: string;
      matchesFound?: number;
      seedsPerSecond?: number;
      elapsed?: string;
    },
    description: "Live search metrics panel. Shows seeds/sec, matches, status.",
  },
  ErrorBanner: {
    props: {} as {
      message: string;
      onDismiss?: boolean; // if true, show X button
    },
    description: "Error state banner. Dismissible if onDismiss is true.",
  },
  LoadingPulse: {
    props: {} as { text?: string },
    description: "Loading skeleton with optional text.",
  },

  // ── Results ──
  SeedCard: {
    props: {} as {
      seed: string;
      score?: number;
      rank?: number;
      highlights?: string[];
      jokers?: string[];
      edition?: string;
      onClick?: boolean; // if true, seed is clickable
    },
    description:
      "A seed result card. Shows seed code, score, jokers, highlights. Tap to expand.",
  },
  SeedList: {
    props: {} as {
      seeds: string[];
      scores?: number[];
      total?: number;
      pageSize?: number;
    },
    description: "Paginated list of seed results. Use when results > 20.",
  },
  JokerBadge: {
    props: {} as {
      name: string;
      edition?: "Foil" | "Holographic" | "Polychrome" | "Negative";
      rarity?: "Common" | "Uncommon" | "Rare" | "Legendary";
    },
    description: "Small joker name pill with rarity color.",
  },
  EditionBadge: {
    props: {} as {
      edition: "Foil" | "Holographic" | "Polychrome" | "Negative";
    },
    description: "Edition badge with appropriate color (blue, green, purple, red).",
  },

  // ── Game Cards (via jaml-ui) ──
  JamlGameCard: {
    props: {} as {
      type: "joker" | "consumable" | "playing";
      card: {
        name: string;
        edition?: string;
        seal?: string;
        isEternal?: boolean;
        isPerishable?: boolean;
        isRental?: boolean;
        scale?: number;
      };
    },
    description: "Renders a real Balatro card using jaml-ui's sprite system.",
  },

  // ── Mascot ──
  JammyMascot: {
    props: {} as {
      mood?: "idle" | "happy" | "surprised";
      size?: number;
      menuItems?: { label: string; action: string; tone?: BadgeTone }[];
      onMenuAction?: boolean; // if true, the mascot emits actions
    },
    description:
      "Jammy — the whimsical seed-mascot. Tap to open an orbital menu of actions.",
  },
  JammyOrbitalMenu: {
    props: {} as {
      items: { label: string; action: string; tone?: BadgeTone }[];
      radius?: number;
    },
    description: "Radial menu orbiting a center point. Used by JammyMascot.",
  },

  // ── Encyclopedia / Reference ──
  JokerCard: {
    props: {} as {
      name: string;
      showSynergies?: boolean;
    },
    description:
      "Full joker info card: name, rarity, cost, effect, strategy, synergies. Queries the knowledge base.",
  },
  SynergyCard: {
    props: {} as {
      name: string;
    },
    description:
      "Synergy guide card: jokers involved, setup steps, math breakdown, boss counters, difficulty rating.",
  },
  BossBlindCard: {
    props: {} as {
      name: string;
    },
    description:
      "Boss blind info card: effect, category, threat level, counters, JAML filter string.",
  },
  DeckCard: {
    props: {} as {
      name: string;
    },
    description:
      "Deck info card: effect, strategy, synergies, difficulty rating.",
  },
  StakeCard: {
    props: {} as {
      name: string;
    },
    description:
      "Stake difficulty card: effect, strategy, difficulty rating.",
  },
  StrategyAdvisor: {
    props: {} as {
      jokers: string[];
    },
    description:
      "Takes a list of joker names and recommends strategies, warns about boss blinds, and suggests synergies.",
  },
});

export type BalatroCatalog = typeof balatroCatalog;
export type BalatroComponentName = keyof BalatroCatalog;

/**
 * Extract the props type for a given catalog component.
 */
export type CatalogProps<K extends BalatroComponentName> =
  BalatroCatalog[K] extends { props: infer P } ? P : never;
