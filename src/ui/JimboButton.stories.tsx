import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboButton } from "./JimboButton.js";

const meta: Meta<typeof JimboButton> = {
  title: "Primitives/Actions/JimboButton",
  component: JimboButton,
};
export default meta;
type Story = StoryObj<typeof JimboButton>;

export const Tones: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 12, gridAutoFlow: "column" }}>
      <JimboButton tone="orange">Orange</JimboButton>
      <JimboButton tone="red">Red</JimboButton>
      <JimboButton tone="blue">Blue</JimboButton>
      <JimboButton tone="green">Green</JimboButton>
    </div>
  ),
};

export const Sizes: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 12, gridAutoFlow: "column", alignItems: "center" }}>
      <JimboButton size="xs">XS</JimboButton>
      <JimboButton size="sm">SM</JimboButton>
      <JimboButton size="md">MD</JimboButton>
      <JimboButton size="lg">LG</JimboButton>
    </div>
  ),
};

export const Disabled: Story = {
  render: () => <JimboButton disabled>Disabled</JimboButton>,
};

export const FullWidth: Story = {
  render: () => (
    <div style={{ width: 320 }}>
      <JimboButton fullWidth tone="red">Search</JimboButton>
    </div>
  ),
};
