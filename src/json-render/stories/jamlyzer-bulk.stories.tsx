import type { Meta, StoryObj } from "@storybook/react-vite";
import { JamlyzerBulk } from "../../components/JamlyzerBulk.js";
import fixture from "./fixtures/jamlyzer-aaaaaaaa.json";

const meta: Meta<typeof JamlyzerBulk> = {
  title: "Screens/Jamlyzer/Bulk",
  component: JamlyzerBulk,
  parameters: {
    layout: "fullscreen",
  },
};

export default meta;
type Story = StoryObj<typeof JamlyzerBulk>;

const sampleJaml = `deck: Red
stake: White
should:
  - joker: WeeJoker
    score: 1
  - tarot: The Fool
    score: 1
must:
  - boss: The Hook
mustNot:
  - voucher: Overstock
`;

export const Default: Story = {
  args: {
    results: [
      { ...(fixture as unknown as Parameters<typeof JamlyzerBulk>[0]["results"][number]), seed: "AAAAAAAA" },
      { ...(fixture as unknown as Parameters<typeof JamlyzerBulk>[0]["results"][number]), seed: "BBBBBBBB" },
      { ...(fixture as unknown as Parameters<typeof JamlyzerBulk>[0]["results"][number]), seed: "CCCCCCCC" },
    ],
    deck: 0,
    stake: 0,
    jamlText: sampleJaml,
    tallies: [
      [2, 1],
      [0, 3],
      [1, 0],
    ],
  },
};
