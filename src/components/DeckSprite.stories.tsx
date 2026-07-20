import type { Meta, StoryObj } from "@storybook/react-vite";
import { DeckSprite, DECK_SPRITE_POS, STAKE_SPRITE_POS } from "./DeckSprite.js";

const meta: Meta<typeof DeckSprite> = {
  title: "Cards & Sprites/DeckSprite",
  component: DeckSprite,
  args: { deck: "erratic", size: 50 },
};
export default meta;
type Story = StoryObj<typeof DeckSprite>;

export const Default: Story = {};

export const WithStake: Story = {
  args: { deck: "plasma", stake: "gold" },
};

export const Sizes: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 12, gridAutoFlow: "column", justifyContent: "start", alignItems: "end" }}>
      <DeckSprite deck="anaglyph" size={32} />
      <DeckSprite deck="anaglyph" size={50} />
      <DeckSprite deck="anaglyph" size={80} />
      <DeckSprite deck="anaglyph" size={128} />
    </div>
  ),
};

export const AllDecks: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 12, gridTemplateColumns: "repeat(6, 64px)" }}>
      {Object.keys(DECK_SPRITE_POS).map((deck) => (
        <div key={deck} style={{ display: "grid", gap: 4, justifyItems: "center" }}>
          <DeckSprite deck={deck} size={64} />
          <span style={{ fontSize: 11 }}>{deck}</span>
        </div>
      ))}
    </div>
  ),
};

export const AllStakes: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 12, gridTemplateColumns: "repeat(4, 64px)" }}>
      {Object.keys(STAKE_SPRITE_POS).map((stake) => (
        <div key={stake} style={{ display: "grid", gap: 4, justifyItems: "center" }}>
          <DeckSprite deck="red" stake={stake} size={64} />
          <span style={{ fontSize: 11 }}>{stake}</span>
        </div>
      ))}
    </div>
  ),
};

/** Name normalization — case and a trailing "Deck"/"Stake" are tolerated. */
export const NameNormalization: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 12, gridAutoFlow: "column", justifyContent: "start" }}>
      <DeckSprite deck="Erratic Deck" size={64} />
      <DeckSprite deck="ERRATIC" size={64} />
      <DeckSprite deck="erratic" size={64} />
      <DeckSprite deck="not-a-real-deck" size={64} />
    </div>
  ),
};
