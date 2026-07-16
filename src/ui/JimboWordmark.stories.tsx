import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboWordmark } from "./JimboWordmark.js";

const meta: Meta<typeof JimboWordmark> = {
  title: "ui/JimboWordmark",
  component: JimboWordmark,
};
export default meta;
type Story = StoryObj<typeof JimboWordmark>;

export const Default: Story = {
  render: () => <JimboWordmark title="JAML" sub="Jimbo's Ante Markup Language" />,
};
