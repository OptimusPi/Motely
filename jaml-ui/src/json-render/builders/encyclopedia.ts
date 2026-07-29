import { JsonNode } from "../engine.js";
import { getRecommendedSynergies } from "../knowledge/synergies.js";

/**
 * Build a json-render spec for the encyclopedia view.
 *
 * Shows joker info, synergies, and strategy advice for a set of jokers.
 */

export interface EncyclopediaParams {
  jokers: string[];
  decks?: string[];
  stakes?: string[];
  bosses?: string[];
  showStrategies?: boolean;
}

export function buildEncyclopediaSpec(params: EncyclopediaParams): JsonNode {
  const children: JsonNode[] = [];

  // ── Joker Cards ──
  if (params.jokers.length > 0) {
    const jokerCards: JsonNode[] = params.jokers.map((name) => ({
      type: "JokerCard",
      props: { name, showSynergies: true },
    }));

    children.push({
      type: "Panel",
      props: { title: "Jokers", variant: "accent" },
      children: [
        {
          type: "Grid",
          props: { columns: 1, gap: 12 },
          children: jokerCards,
        },
      ],
    });
  }

  // ── Strategy Advisor ──
  if (params.showStrategies && params.jokers.length > 0) {
    children.push({
      type: "StrategyAdvisor",
      props: { jokers: params.jokers },
    });
  }

  // ── Synergy Cards ──
  const synergies = getRecommendedSynergies(params.jokers);
  if (synergies.length > 0) {
    const synergyCards: JsonNode[] = synergies.map((s) => ({
      type: "SynergyCard",
      props: { name: s.name },
    }));

    children.push({
      type: "Panel",
      props: { title: "Synergies", variant: "accent" },
      children: [
        {
          type: "Grid",
          props: { columns: 1, gap: 12 },
          children: synergyCards,
        },
      ],
    });
  }

  // ── Deck Cards ──
  if (params.decks && params.decks.length > 0) {
    const deckCards: JsonNode[] = params.decks.map((name) => ({
      type: "DeckCard",
      props: { name },
    }));

    children.push({
      type: "Panel",
      props: { title: "Decks" },
      children: [
        {
          type: "Grid",
          props: { columns: 1, gap: 12 },
          children: deckCards,
        },
      ],
    });
  }

  // ── Stake Cards ──
  if (params.stakes && params.stakes.length > 0) {
    const stakeCards: JsonNode[] = params.stakes.map((name) => ({
      type: "StakeCard",
      props: { name },
    }));

    children.push({
      type: "Panel",
      props: { title: "Stakes" },
      children: [
        {
          type: "Grid",
          props: { columns: 1, gap: 12 },
          children: stakeCards,
        },
      ],
    });
  }

  // ── Boss Blind Cards ──
  if (params.bosses && params.bosses.length > 0) {
    const bossCards: JsonNode[] = params.bosses.map((name) => ({
      type: "BossBlindCard",
      props: { name },
    }));

    children.push({
      type: "Panel",
      props: { title: "Boss Blinds", variant: "muted" },
      children: [
        {
          type: "Grid",
          props: { columns: 1, gap: 12 },
          children: bossCards,
        },
      ],
    });
  }

  return {
    type: "Stack",
    props: { gap: 16 },
    children,
  };
}
