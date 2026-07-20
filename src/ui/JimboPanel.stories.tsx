import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboPanel } from "./JimboPanel.js";

const meta: Meta<typeof JimboPanel> = {
  title: "Primitives/Layout/JimboPanel",
  component: JimboPanel,
};
export default meta;
type Story = StoryObj<typeof JimboPanel>;

export const Default: Story = {
  render: () => (
    <JimboPanel style={{ width: 320 }}>
      A Jimbo panel with no title — freestyle content.
    </JimboPanel>
  ),
};

export const WithTitle: Story = {
  render: () => (
    <JimboPanel title="Search Results" tone="blue" style={{ width: 320 }}>
      Rounded, bordered surface with a section tag.
    </JimboPanel>
  ),
};
