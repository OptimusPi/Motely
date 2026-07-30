import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboBadge } from "./JimboBadge.js";

const meta: Meta<typeof JimboBadge> = {
  title: "Primitives/Display/JimboBadge",
  component: JimboBadge,
};
export default meta;
type Story = StoryObj<typeof JimboBadge>;

export const Sizes: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 8, gridAutoFlow: "column", alignItems: "center" }}>
      <JimboBadge size="sm">1 of 5</JimboBadge>
      <JimboBadge>5 seeds</JimboBadge>
    </div>
  ),
};

export const Tones: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 8, gridAutoFlow: "column" }}>
      <JimboBadge tone="dark">Dark</JimboBadge>
      <JimboBadge tone="grey">Grey</JimboBadge>
    </div>
  ),
};
