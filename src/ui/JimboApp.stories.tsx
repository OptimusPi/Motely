import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboApp } from "./JimboApp.js";
import { JimboText } from "./jimboText.js";

const meta: Meta<typeof JimboApp> = {
  title: "Primitives/Layout/JimboApp",
  component: JimboApp,
  parameters: { layout: "fullscreen" },
};
export default meta;
type Story = StoryObj<typeof JimboApp>;

export const Embed: Story = {
  render: () => (
    <JimboApp variant="embed">
      <JimboText size="md" tone="white">
        Fixed 320x540 MCP shell.
      </JimboText>
    </JimboApp>
  ),
};
