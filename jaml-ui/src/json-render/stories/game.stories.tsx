import type { Meta, StoryObj } from "@storybook/react-vite";
import { Stack } from "../components/layout.js";
import { JamlGameCard } from "../components/game.js";

const meta: Meta<typeof JamlGameCard> = {
  title: "Wire Format/Game",
  component: JamlGameCard,
};

export default meta;

export const JokerBlueprint: StoryObj<typeof JamlGameCard> = {
  args: {
    type: "joker",
    card: { name: "Blueprint", edition: "Foil", isEternal: true },
  },
};

export const JokerLegendary: StoryObj<typeof JamlGameCard> = {
  args: {
    type: "joker",
    card: { name: "Perkeo" },
  },
};

export const PlayingCard: StoryObj<typeof JamlGameCard> = {
  args: {
    type: "playing",
    card: { name: "Ace of Spades", edition: "Polychrome", seal: "Gold" },
  },
};

export const Consumable: StoryObj<typeof JamlGameCard> = {
  args: {
    type: "consumable",
    card: { name: "The Emperor" },
  },
};

export const CardGallery: StoryObj<typeof JamlGameCard> = {
  render: () => (
    <Stack gap={16}>
      <JamlGameCard type="joker" card={{ name: "Blueprint" }} />
      <JamlGameCard type="joker" card={{ name: "Brainstorm" }} />
      <JamlGameCard type="playing" card={{ name: "10 of Hearts" }} />
      <JamlGameCard type="consumable" card={{ name: "The Fool" }} />
    </Stack>
  ),
};
