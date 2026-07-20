import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboText } from "./jimboText.js";

const meta: Meta<typeof JimboText> = {
  title: "Primitives/Display/JimboText",
  component: JimboText,
};
export default meta;
type Story = StoryObj<typeof JimboText>;

export const Sizes: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 8 }}>
      <JimboText size="micro">Micro text</JimboText>
      <JimboText size="xs">Extra small text</JimboText>
      <JimboText size="sm">Small text</JimboText>
      <JimboText size="md">Medium text</JimboText>
      <JimboText size="lg">Large text</JimboText>
      <JimboText size="xl">Extra large text</JimboText>
      <JimboText size="display">Display text</JimboText>
    </div>
  ),
};

export const Tones: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 8, background: "var(--j-darkest)", padding: 12 }}>
      <JimboText tone="white">White</JimboText>
      <JimboText tone="grey">Grey</JimboText>
      <JimboText tone="gold">Gold</JimboText>
      <JimboText tone="red">Red</JimboText>
      <JimboText tone="blue">Blue</JimboText>
      <JimboText tone="green">Green</JimboText>
      <JimboText tone="orange">Orange</JimboText>
      <JimboText tone="purple">Purple</JimboText>
    </div>
  ),
};
