import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboListItem } from "./JimboListItem.js";

const meta: Meta<typeof JimboListItem> = {
  title: "Primitives/Display/JimboListItem",
  component: JimboListItem,
};
export default meta;
type Story = StoryObj<typeof JimboListItem>;

export const Default: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 4, width: 240 }}>
      <JimboListItem active>Selected item</JimboListItem>
      <JimboListItem>Another item</JimboListItem>
      <JimboListItem>Third item</JimboListItem>
    </div>
  ),
};
