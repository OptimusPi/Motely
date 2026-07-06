import type { Meta, StoryObj } from "@storybook/react-vite";
import { JamlyzerView } from "../../components/JamlyzerView.js";
import fixture from "./fixtures/jamlyzer-aaaaaaaa.json";

const meta: Meta<typeof JamlyzerView> = {
  title: "Jamlyzer / JamlyzerView",
  component: JamlyzerView,
  parameters: {
    layout: "fullscreen",
  },
};

export default meta;
type Story = StoryObj<typeof JamlyzerView>;

export const Default: Story = {
  args: {
    result: fixture as unknown as Parameters<typeof JamlyzerView>[0]["result"],
    deck: 0,
    stake: 0,
    maxAnte: 8,
  },
};
