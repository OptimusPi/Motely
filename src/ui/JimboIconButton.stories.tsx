import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboIconButton } from "./JimboIconButton.js";
import { FiX } from "react-icons/fi";

const meta: Meta<typeof JimboIconButton> = {
  title: "Primitives/Actions/JimboIconButton",
  component: JimboIconButton,
};
export default meta;
type Story = StoryObj<typeof JimboIconButton>;

export const Default: Story = {
  render: () => (
    <JimboIconButton aria-label="Close" title="Close">
      <FiX />
    </JimboIconButton>
  ),
};

export const Destructive: Story = {
  render: () => (
    <JimboIconButton tone="destructive" aria-label="Delete" title="Delete">
      <FiX />
    </JimboIconButton>
  ),
};
