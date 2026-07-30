import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboBalatroFooter } from "./JimboBalatroFooter.js";

const meta: Meta<typeof JimboBalatroFooter> = {
  title: "Foundations/Balatro Footer",
  component: JimboBalatroFooter,
  parameters: { layout: "fullscreen" },
};

export default meta;
type Story = StoryObj<typeof JimboBalatroFooter>;

export const Default: Story = {
  args: {},
  render: (args) => (
    <div style={{ position: "relative", height: 120, background: "#0c1818" }}>
      <JimboBalatroFooter {...args} style={{ position: "absolute", bottom: 0, left: 0, right: 0 }} />
    </div>
  ),
};

export const Hidden: Story = {
  args: { hidden: true },
  render: (args) => (
    <div style={{ position: "relative", height: 120, background: "#0c1818" }}>
      <JimboBalatroFooter {...args} style={{ position: "absolute", bottom: 0, left: 0, right: 0 }} />
    </div>
  ),
};
