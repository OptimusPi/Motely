import type { Meta, StoryObj } from "@storybook/react-vite";
import { JamlyzerView } from "../../components/JamlyzerView.js";
import { JimboApp } from "../../ui/JimboApp.js";
import fixture from "./fixtures/jamlyzer-aaaaaaaa.json";

const meta: Meta<typeof JamlyzerView> = {
  title: "Screens/Jamlyzer/View",
  component: JamlyzerView,
  parameters: {
    layout: "fullscreen",
  },
  decorators: [
    (Story) => (
      <JimboApp>
        <Story />
      </JimboApp>
    ),
  ],
};

export default meta;
type Story = StoryObj<typeof JamlyzerView>;

export const Default: Story = {
  args: {
    result: fixture as unknown as Parameters<typeof JamlyzerView>[0]["result"],
    deck: 0,
    stake: 0,
  },
};

export const WithClauseHighlighting: Story = {
  args: {
    result: fixture as unknown as Parameters<typeof JamlyzerView>[0]["result"],
    deck: 0,
    stake: 0,
    jamlText: `deck: Red\nstake: White\nshould:\n  - joker: WeeJoker\n    score: 1\n  - tarot: The Fool\n    score: 1\nmust:\n  - boss: The Hook\nmustNot:\n  - voucher: Overstock\n`,
    tallies: [2, 1],
  },
};
