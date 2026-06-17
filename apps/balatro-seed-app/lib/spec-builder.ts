import { z } from "zod";
import type { SpecType } from "@json-render/react";

/**
 * Build a json-render Spec from search results.
 * Converts raw Motely search data into a structured UI tree.
 */
export function buildSearchSpec(params: {
  status: "idle" | "running" | "completed" | "error";
  seedsSearched?: string;
  matchesFound?: number;
  seedsPerSecond?: number;
  error?: string | null;
  results: Array<{
    seed: string;
    score: number;
    highlights?: string[];
    jokers?: string[];
  }>;
}): SpecType {
  const elements: Record<string, any> = {};
  const children: string[] = [];

  // Search stats panel
  const statsId = "search-stats";
  elements[statsId] = {
    type: "SearchStats",
    props: {
      status: params.status,
      seedsSearched: params.seedsSearched,
      matchesFound: params.matchesFound,
      seedsPerSecond: params.seedsPerSecond,
    },
    children: [],
  };
  children.push(statsId);

  // Error panel
  if (params.error) {
    const errorId = "search-error";
    elements[errorId] = {
      type: "Panel",
      props: {
        title: "Error",
        variant: "muted" as const,
      },
      children: ["error-text"],
    };
    elements["error-text"] = {
      type: "Text",
      props: { body: params.error, variant: "muted" as const },
      children: [],
    };
    children.push(errorId);
  }

  // Results grid
  if (params.results.length > 0) {
    const resultsPanelId = "results-panel";
    const gridId = "results-grid";

    elements[resultsPanelId] = {
      type: "Panel",
      props: {
        title: `Results (${params.matchesFound ?? params.results.length})`,
        subtitle: params.status === "running" ? "More matches arriving…" : undefined,
      },
      children: [gridId],
    };

    const gridChildren: string[] = [];
    params.results.forEach((r, i) => {
      const cardId = `seed-card-${i}`;
      elements[cardId] = {
        type: "SeedCard",
        props: {
          seed: r.seed,
          score: r.score,
          rank: i + 1,
          highlights: r.highlights,
          jokers: r.jokers,
        },
        children: [],
      };
      gridChildren.push(cardId);
    });

    elements[gridId] = {
      type: "Grid",
      props: { columns: 3, gap: 16 },
      children: gridChildren,
    };

    children.push(resultsPanelId);
  }

  // Empty state
  if (params.status === "completed" && params.results.length === 0) {
    const emptyId = "empty-state";
    elements[emptyId] = {
      type: "Panel",
      props: {
        title: "No matches",
        variant: "muted" as const,
      },
      children: ["empty-text"],
    };
    elements["empty-text"] = {
      type: "Text",
      props: {
        body: "Try relaxing your filter or increasing the search count.",
        variant: "muted" as const,
      },
      children: [],
    };
    children.push(emptyId);
  }

  return {
    root: "root",
    elements: {
      root: {
        type: "Stack",
        props: { gap: 16 },
        children,
      },
      ...elements,
    },
  };
}

/**
 * Build a spec for a single seed analysis result.
 */
export function buildAnalyzeSpec(params: {
  seed: string;
  deck: string;
  stake: string;
  antes: Array<{
    ante: number;
    boss: string;
    shopCount?: number;
    packCount?: number;
  }>;
}): SpecType {
  return {
    root: "root",
    elements: {
      root: {
        type: "Stack",
        props: { gap: 16 },
        children: ["header", "route"],
      },
      header: {
        type: "Panel",
        props: { title: "Seed Analysis" },
        children: ["seed-info"],
      },
      "seed-info": {
        type: "Stack",
        props: { gap: 8 },
        children: ["seed-code", "seed-meta"],
      },
      "seed-code": {
        type: "Text",
        props: { body: params.seed, variant: "code" as const },
        children: [],
      },
      "seed-meta": {
        type: "Stack",
        props: { gap: 4, align: "start" as const },
        children: ["deck-badge", "stake-badge"],
      },
      "deck-badge": {
        type: "Badge",
        props: { label: `Deck: ${params.deck}`, variant: "info" as const },
        children: [],
      },
      "stake-badge": {
        type: "Badge",
        props: { label: `Stake: ${params.stake}`, variant: "info" as const },
        children: [],
      },
      route: {
        type: "FullRoute",
        props: {
          seed: params.seed,
          antes: params.antes,
        },
        children: [],
      },
    },
  };
}

/**
 * Build a spec for erratic deck analysis.
 */
export function buildErraticSpec(params: {
  seed: string;
  cards: Array<{ rank: string; suit: string }>;
  suits?: Record<string, number>;
  ranks?: Record<string, number>;
  erraticScore?: number;
}): SpecType {
  return {
    root: "root",
    elements: {
      root: {
        type: "Stack",
        props: { gap: 16 },
        children: ["erratic-deck"],
      },
      "erratic-deck": {
        type: "ErraticDeck",
        props: {
          seed: params.seed,
          cards: params.cards,
          suits: params.suits,
          ranks: params.ranks,
          erraticScore: params.erraticScore,
        },
        children: [],
      },
    },
  };
}

/**
 * Build a chat message spec.
 */
export function buildChatSpec(messages: Array<{ role: "user" | "assistant" | "system"; content: string }>): SpecType {
  const elements: Record<string, any> = {};
  const children: string[] = [];

  messages.forEach((msg, i) => {
    const id = `msg-${i}`;
    elements[id] = {
      type: "ChatMessage",
      props: {
        role: msg.role,
        content: msg.content,
      },
      children: [],
    };
    children.push(id);
  });

  return {
    root: "root",
    elements: {
      root: {
        type: "Stack",
        props: { gap: 8 },
        children,
      },
      ...elements,
    },
  };
}

/**
 * Build an empty / loading spec.
 */
export function buildLoadingSpec(message: string): SpecType {
  return {
    root: "root",
    elements: {
      root: {
        type: "Panel",
        props: { title: message, variant: "muted" as const },
        children: [],
      },
    },
  };
}
