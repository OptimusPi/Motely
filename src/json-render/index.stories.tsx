import React from "react";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { buildEncyclopediaSpec } from "./index.js";
import { render, balatroRegistry } from "./index.js";
import type { JsonNode } from "./index.js";
import "../ui/jimbo-tokens.css";

/**
 * json-render — JSON-to-React engine for AI-generated Balatro UI.
 *
 * These stories show the full pipeline: write a JSON spec → render it.
 */
const meta = {
  title: "json-render / Engine",
  parameters: { layout: "fullscreen" },
} satisfies Meta;

export default meta;

type Story = StoryObj<typeof meta>;

function RenderStory({ spec }: { spec: JsonNode }) {
  return (
    <div
      style={{
        background: "var(--j-darkest)",
        minHeight: "100vh",
        padding: 24,
      }}
    >
      {render(spec, balatroRegistry)}
    </div>
  );
}

export const SearchResults: Story = {
  render: () => (
    <RenderStory
      spec={{
        type: "Stack",
        props: { gap: 16 },
        children: [
          {
            type: "SearchStats",
            props: {
              status: "running",
              seedsSearched: "1,420,069",
              matchesFound: 3,
              seedsPerSecond: 69420,
            },
          },
          {
            type: "Panel",
            props: { title: "Results (3)", subtitle: "More matches arriving…" },
            children: [
              {
                type: "Grid",
                props: { columns: 1, gap: 12 },
                children: [
                  {
                    type: "SeedCard",
                    props: {
                      seed: "ALEEB",
                      score: 420,
                      rank: 1,
                      jokers: ["Blueprint", "DNA"],
                      edition: "Foil",
                      onClick: true,
                    },
                  },
                  {
                    type: "SeedCard",
                    props: {
                      seed: "BEPIS",
                      score: 380,
                      rank: 2,
                      jokers: ["Midas", "Perkeo"],
                      highlights: ["Legendary"],
                      onClick: true,
                    },
                  },
                  {
                    type: "SeedCard",
                    props: {
                      seed: "COOLG",
                      score: 69,
                      rank: 3,
                      jokers: ["Joker"],
                      onClick: true,
                    },
                  },
                ],
              },
            ],
          },
        ],
      }}
    />
  ),
};

export const ErrorState: Story = {
  render: () => (
    <RenderStory
      spec={{
        type: "Stack",
        props: { gap: 16 },
        children: [
          {
            type: "SearchStats",
            props: {
              status: "error",
              seedsSearched: "50,000",
              matchesFound: 0,
            },
          },
          {
            type: "ErrorBanner",
            props: {
              message: "Motely WASM failed to boot. Check /motely-wasm/bin is served.",
              onDismiss: true,
            },
          },
        ],
      }}
    />
  ),
};

export const AnalyzerView: Story = {
  render: () => (
    <RenderStory
      spec={{
        type: "Stack",
        props: { gap: 16 },
        children: [
          {
            type: "Panel",
            props: { title: "Seed: ALEEB", variant: "accent" },
            children: [
              {
                type: "Text",
                props: { body: "Score: 420", variant: "title" },
              },
            ],
          },
          {
            type: "Panel",
            props: { title: "Jokers" },
            children: [
              {
                type: "Grid",
                props: { columns: 3, gap: 12 },
                children: [
                  {
                    type: "JokerBadge",
                    props: { name: "Blueprint", rarity: "Rare", edition: "Foil" },
                  },
                  {
                    type: "JokerBadge",
                    props: { name: "DNA", rarity: "Uncommon" },
                  },
                  {
                    type: "JokerBadge",
                    props: { name: "Midas", rarity: "Legendary", edition: "Polychrome" },
                  },
                ],
              },
            ],
          },
          {
            type: "Panel",
            props: { title: "Shop" },
            children: [
              {
                type: "Grid",
                props: { columns: 4, gap: 8 },
                children: [
                  { type: "Badge", props: { label: "Tarot", tone: "purple" } },
                  { type: "Badge", props: { label: "Planet", tone: "blue" } },
                  { type: "Badge", props: { label: "Spectral", tone: "orange" } },
                  { type: "Badge", props: { label: "Buffoon", tone: "red" } },
                ],
              },
            ],
          },
          {
            type: "Panel",
            props: { title: "Notes", variant: "muted" },
            children: [
              {
                type: "Text",
                props: { body: "• First blind: Small Blind (no modifiers)", variant: "muted" },
              },
              {
                type: "Text",
                props: { body: "• Ante 1 shop: 2x Tarot, 1x Planet, 1x Buffoon", variant: "muted" },
              },
              {
                type: "Text",
                props: { body: "• Blueprint copies DNA — doubled jokers!", variant: "accent" },
              },
            ],
          },
        ],
      }}
    />
  ),
};

export const LoadingState: Story = {
  render: () => (
    <RenderStory
      spec={{
        type: "Stack",
        props: { gap: 16, align: "center" },
        children: [
          {
            type: "LoadingPulse",
            props: { text: "Booting motely-wasm…" },
          },
        ],
      }}
    />
  ),
};

export const EncyclopediaView: Story = {
  render: () => (
    <RenderStory
      spec={buildEncyclopediaSpec({
        jokers: ["Baron", "Mime", "Blueprint"],
        decks: ["Black", "Painted"],
        stakes: ["Gold"],
        bosses: ["The Plant", "The Manacle"],
        showStrategies: true,
      })}
    />
  ),
};
