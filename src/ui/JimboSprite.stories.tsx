import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboSprite } from "./sprites.js";

const meta: Meta<typeof JimboSprite> = {
  title: "Cards & Sprites/JimboSprite",
  component: JimboSprite,
};
export default meta;
type Story = StoryObj<typeof JimboSprite>;

export const Joker: Story = {
  render: () => <JimboSprite name="Joker" sheet="Jokers" width={71} />,
};

export const BossChip: Story = {
  render: () => <JimboSprite name="The Hook" sheet="BlindChips" width={40} />,
};

export const Mystery: Story = {
  render: () => <JimboSprite name="Not A Real Item" sheet="Jokers" width={71} />,
};
