import { JsonNode } from "../engine.js";

/**
 * Build a json-render spec for seed analysis results.
 *
 * Shows the full breakdown of a single seed: jokers, shop, blinds, etc.
 */

export interface AnalyzerResult {
  seed: string;
  jokers: Array<{
    name: string;
    edition?: string;
    seal?: string;
    isEternal?: boolean;
  }>;
  shopItems?: Array<{
    name: string;
    type: string;
    edition?: string;
  }>;
  score?: number;
  notes?: string[];
}

export function buildAnalyzerSpec(result: AnalyzerResult): JsonNode {
  const children: JsonNode[] = [];

  // ── Header ──
  children.push({
    type: "Panel",
    props: { title: `Seed: ${result.seed}`, variant: "accent" },
    children: [
      {
        type: "Text",
        props: { body: `Score: ${result.score ?? "N/A"}`, variant: "title" },
      },
    ],
  });

  children.push({ type: "Spacer", props: { size: 12 } });

  // ── Jokers ──
  if (result.jokers.length > 0) {
    const jokerCards: JsonNode[] = result.jokers.map((j) => ({
      type: "JamlGameCard",
      props: {
        type: "joker",
        card: {
          name: j.name,
          edition: j.edition,
          seal: j.seal,
          isEternal: j.isEternal,
        },
        scale: 0.8,
      },
    }));

    children.push({
      type: "Panel",
      props: { title: "Jokers" },
      children: [
        {
          type: "Grid",
          props: { columns: 3, gap: 12 },
          children: jokerCards,
        },
      ],
    });
  }

  children.push({ type: "Spacer", props: { size: 12 } });

  // ── Shop ──
  if (result.shopItems && result.shopItems.length > 0) {
    const shopBadges: JsonNode[] = result.shopItems.map((s) => ({
      type: "Badge",
      props: { label: s.name, tone: "blue" },
    }));

    children.push({
      type: "Panel",
      props: { title: "Shop" },
      children: [
        {
          type: "Grid",
          props: { columns: 4, gap: 8 },
          children: shopBadges,
        },
      ],
    });
  }

  children.push({ type: "Spacer", props: { size: 12 } });

  // ── Notes ──
  if (result.notes && result.notes.length > 0) {
    const noteTexts: JsonNode[] = result.notes.map((n) => ({
      type: "Text",
      props: { body: `• ${n}`, variant: "muted" },
    }));

    children.push({
      type: "Panel",
      props: { title: "Notes", variant: "muted" },
      children: noteTexts,
    });
  }

  return {
    type: "Stack",
    props: { gap: 16 },
    children,
  };
}
