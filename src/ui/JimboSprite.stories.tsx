import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboSprite } from "./sprites.js";

const meta: Meta<typeof JimboSprite> = {
  title: "Cards & Sprites/JimboSprite",
  component: JimboSprite,
};
export default meta;
type Story = StoryObj<typeof JimboSprite>;

/* Real size = 2x the native cell (71x95 cards, 34x34 chips), the in-game
   look. Keep widths integer multiples of the cell or the pixel art smears. */
export const Joker: Story = {
  render: () => <JimboSprite name="Joker" sheet="Jokers" width={142} />,
};

export const BossChip: Story = {
  render: () => <JimboSprite name="The Hook" sheet="BlindChips" width={68} />,
};

export const Mystery: Story = {
  render: () => <JimboSprite name="Not A Real Item" sheet="Jokers" width={142} />,
};
