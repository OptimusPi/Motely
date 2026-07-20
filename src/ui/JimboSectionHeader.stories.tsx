import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboSectionHeader } from "./JimboSectionHeader.js";

const meta: Meta<typeof JimboSectionHeader> = {
  title: "Primitives/Layout/JimboSectionHeader",
  component: JimboSectionHeader,
};
export default meta;
type Story = StoryObj<typeof JimboSectionHeader>;

export const Tones: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 12, width: 320 }}>
      <JimboSectionHeader label="Boss & Voucher" tone="gold" />
      <JimboSectionHeader label="Tags" tone="green" />
      <JimboSectionHeader label="Shop Queue" tone="red" />
    </div>
  ),
};
