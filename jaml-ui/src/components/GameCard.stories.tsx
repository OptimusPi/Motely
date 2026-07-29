import type { Meta, StoryObj } from "@storybook/react-vite";
import { JamlGameCard, JamlVoucher, JamlTag, JamlBoss } from "./GameCard.js";

const meta: Meta<typeof JamlGameCard> = {
  title: "Cards & Sprites/GameCard",
  component: JamlGameCard,
  /* scale 2 = 142x190, the in-game look. Native cell is 71x95; keep scales
     integer — fractional multiples resample the pixel art into mush. */
  args: { card: { name: "Joker", scale: 2 }, type: "joker" },
};
export default meta;
type Story = StoryObj<typeof JamlGameCard>;

export const Joker: Story = {};

export const Consumable: Story = {
  args: { card: { name: "The Fool" }, type: "consumable" },
};

export const PlayingCard: Story = {
  args: { card: { name: "Ace of Spades" }, type: "playing" },
};

/** All four editions on the same joker, so the overlay layer is comparable. */
export const Editions: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 16, gridAutoFlow: "column", justifyContent: "start" }}>
      <JamlGameCard card={{ name: "Joker", scale: 2 }} type="joker" />
      <JamlGameCard card={{ name: "Joker", edition: "Foil", scale: 2 }} type="joker" />
      <JamlGameCard card={{ name: "Joker", edition: "Holographic", scale: 2 }} type="joker" />
      <JamlGameCard card={{ name: "Joker", edition: "Polychrome", scale: 2 }} type="joker" />
      <JamlGameCard card={{ name: "Joker", edition: "Negative", scale: 2 }} type="joker" />
    </div>
  ),
};

/** Sticker layers — eternal, perishable, rental, and all three stacked. */
export const Stickers: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 16, gridAutoFlow: "column", justifyContent: "start" }}>
      <JamlGameCard card={{ name: "Joker", isEternal: true, scale: 2 }} type="joker" />
      <JamlGameCard card={{ name: "Joker", isPerishable: true, scale: 2 }} type="joker" />
      <JamlGameCard card={{ name: "Joker", isRental: true, scale: 2 }} type="joker" />
      <JamlGameCard
        card={{ name: "Joker", isEternal: true, isPerishable: true, isRental: true, scale: 2 }}
        type="joker"
      />
    </div>
  ),
};

/** Playing-card enhancements and seals stack over the rank/suit layer. */
export const EnhancementsAndSeals: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 16, gridAutoFlow: "column", justifyContent: "start" }}>
      <JamlGameCard card={{ name: "Ace of Hearts", rank: "Ace", suit: "Hearts", scale: 2 }} type="playing" />
      <JamlGameCard
        card={{ name: "Ace of Hearts", rank: "Ace", suit: "Hearts", enhancements: ["Glass"], scale: 2 }}
        type="playing"
      />
      <JamlGameCard
        card={{ name: "Ace of Hearts", rank: "Ace", suit: "Hearts", seal: "Red Seal", scale: 2 }}
        type="playing"
      />
      <JamlGameCard
        card={{ name: "Ace of Hearts", rank: "Ace", suit: "Hearts", enhancements: ["Steel"], seal: "Gold Seal", scale: 2 }}
        type="playing"
      />
    </div>
  ),
};

/** Rank/suit parsing accepts long form, short form, and explicit props. */
export const NameParsing: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 16, gridAutoFlow: "column", justifyContent: "start" }}>
      <JamlGameCard card={{ name: "King of Clubs", scale: 2 }} type="playing" />
      <JamlGameCard card={{ name: "KC", scale: 2 }} type="playing" />
      <JamlGameCard card={{ name: "10 of Diamonds", scale: 2 }} type="playing" />
      <JamlGameCard card={{ name: "ignored", rank: "Queen", suit: "Spades", scale: 2 }} type="playing" />
    </div>
  ),
};

export const Scales: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 16, gridAutoFlow: "column", justifyContent: "start", alignItems: "end" }}>
      <JamlGameCard card={{ name: "Joker", scale: 1 }} type="joker" />
      <JamlGameCard card={{ name: "Joker", scale: 2 }} type="joker" />
      <JamlGameCard card={{ name: "Joker", scale: 3 }} type="joker" />
    </div>
  ),
};

export const HoverTilt: Story = {
  args: { card: { name: "Joker", scale: 2 }, type: "joker", hoverTilt: true },
};

/** An unrecognized name renders no sprite layers — the card comes back empty. */
export const UnknownName: Story = {
  args: { card: { name: "Not A Real Joker", scale: 2 }, type: "joker" },
};

export const Voucher: StoryObj<typeof JamlVoucher> = {
  render: () => <JamlVoucher voucherName="Overstock" scale={2} />,
};

export const Tag: StoryObj<typeof JamlTag> = {
  render: () => <JamlTag tagName="Rare Tag" scale={2} />,
};

export const Boss: StoryObj<typeof JamlBoss> = {
  render: () => <JamlBoss bossName="The Wall" scale={2} />,
};
