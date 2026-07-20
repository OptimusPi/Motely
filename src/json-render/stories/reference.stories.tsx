import type { Meta, StoryObj } from "@storybook/react-vite";

import {
  JokerCard,
  SynergyCard,
  BossBlindCard,
  DeckCard,
  StakeCard,
  StrategyAdvisor,
} from "../components/reference.js";

const meta: Meta<typeof JokerCard> = {
  title: "Wire Format/Reference",
};

export default meta;

export const JokerBlueprint: StoryObj<typeof JokerCard> = {
  render: () => <JokerCard name="Blueprint" showSynergies />,
};

export const JokerPerkeo: StoryObj<typeof JokerCard> = {
  render: () => <JokerCard name="Perkeo" showSynergies={false} />,
};

export const Synergy: StoryObj<typeof SynergyCard> = {
  render: () => <SynergyCard name="Blueprint + Brainstorm" />,
};

export const BossBlind: StoryObj<typeof BossBlindCard> = {
  render: () => <BossBlindCard name="The Hook" />,
};

export const Deck: StoryObj<typeof DeckCard> = {
  render: () => <DeckCard name="Red Deck" />,
};

export const Stake: StoryObj<typeof StakeCard> = {
  render: () => <StakeCard name="Gold Stake" />,
};

export const Strategy: StoryObj<typeof StrategyAdvisor> = {
  render: () => (
    <StrategyAdvisor jokers={["Blueprint", "Brainstorm", "Perkeo"]} />
  ),
};
