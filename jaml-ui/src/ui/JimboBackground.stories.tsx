import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboBackground } from "./JimboBackground.js";

const meta: Meta<typeof JimboBackground> = {
  title: "Foundations/Background",
  component: JimboBackground,
  parameters: { layout: "fullscreen" },
};

export default meta;
type Story = StoryObj<typeof JimboBackground>;

export const Swirl: Story = {
  render: () => (
    <div style={{ position: "relative", width: "100%", height: "100vh" }}>
      <JimboBackground />
    </div>
  ),
};
