import type { Meta, StoryObj } from "@storybook/react-vite";
import { render } from "../engine.js";
import type { JsonNode } from "../engine.js";
import { balatroRegistry } from "../registry.js";
import { JimboApp } from "../../ui/JimboApp.js";

/**
 * Real rows from the community seed library (list_seeds, 2026-07-19).
 * Nothing here is invented — seed, score, deck, stake and filter are the
 * stored values. Fields the library returns as null stay absent.
 */
const FOUND_SEEDS = [
  { seed: "H95HQCVY", score: 1000, deck: "Ghost", stake: "White", filter: "cola-faucet-v3-early-detonation-whimsy" },
  { seed: "BQ6MGFG8", score: 800, deck: "Ghost", stake: "White", filter: "cola-faucet-ghost" },
  { seed: "ILJYQ7NG", score: 610, deck: "Ghost", stake: "White", filter: null },
  { seed: "R1CZLXJ8", score: 399, deck: "Red", stake: "White", filter: null },
  { seed: "RQ18NZ7U", score: 380, deck: "Anaglyph", stake: "White", filter: "whimsy-dicetricks" },
  { seed: "7KHAAHL5", score: 345, deck: "Anaglyph", stake: "White", filter: "whimsy-dicetricks" },
  { seed: "QWXWNV1R", score: 255, deck: "Ghost", stake: "White", filter: null },
  { seed: "NAT1GH8W", score: 220, deck: "Anaglyph", stake: "White", filter: "nat-oops-copy-dynasty" },
  { seed: "5WP4U311", score: 200, deck: "Anaglyph", stake: "White", filter: "anaglyph-negativetag-skipper" },
  { seed: "WVDWUEAA", score: 198, deck: "Plasma", stake: "White", filter: null },
  { seed: "3LOVEOOG", score: 160, deck: "Anaglyph", stake: "White", filter: "anaglyph-negativetag-skipper-v2" },
  { seed: "POAYQFL1", score: 160, deck: "Red", stake: "White", filter: "perkeo-oops-shitload" },
  { seed: "LOLAEFGT", score: 140, deck: "Red", stake: "White", filter: "lola-perkeo" },
];

function seedCard(row: (typeof FOUND_SEEDS)[number]): JsonNode {
  const body: JsonNode[] = [
    { type: "Text", props: { body: row.seed, variant: "title" } },
    { type: "Spacer", props: { size: 8 } },
    { type: "Text", props: { body: `Score ${row.score}`, variant: "accent" } },
    { type: "Spacer", props: { size: 12 } },
    {
      type: "Grid",
      props: { columns: 2, gap: 8 },
      children: [
        { type: "Badge", props: { label: row.deck, tone: "purple" } },
        { type: "Badge", props: { label: row.stake, tone: "grey" } },
      ],
    },
  ];

  if (row.filter) {
    body.push({ type: "Spacer", props: { size: 12 } });
    body.push({ type: "Text", props: { body: row.filter, variant: "muted" } });
  }

  return {
    type: "Panel",
    props: { title: "Found seed", variant: "accent" },
    children: body,
  };
}

/** The entire screen as data. A server could send exactly this. */
const TRIAGE_SPEC: JsonNode = {
  type: "SwipeDeck",
  props: { width: 280, height: 360 },
  children: FOUND_SEEDS.map(seedCard),
};

const meta: Meta = {
  title: "Json Render / SwipeDeck",
  parameters: { layout: "centered" },
  decorators: [
    (Story) => (
      <JimboApp>
        <Story />
      </JimboApp>
    ),
  ],
};

export default meta;
type Story = StoryObj;

/**
 * Drag left to pass, right to keep. Arrow keys work too, backspace undoes.
 * Touch-dragging is the point — this is built for triaging a search that
 * returned more seeds than anyone will read.
 */
export const Triage: Story = {
  render: () => <>{render(TRIAGE_SPEC, balatroRegistry)}</>,
};

/** The spec above, as the JSON a server would actually put on the wire. */
export const AsWireFormat: Story = {
  render: () => (
    <pre style={{ maxWidth: 520, overflowX: "auto", fontSize: 11, lineHeight: 1.5 }}>
      {JSON.stringify(TRIAGE_SPEC, null, 2)}
    </pre>
  ),
};
