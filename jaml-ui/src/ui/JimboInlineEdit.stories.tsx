import type { Meta, StoryObj } from "@storybook/react-vite";
import { useState } from "react";
import { JimboInlineEdit } from "./JimboInlineEdit.js";

const meta: Meta<typeof JimboInlineEdit> = {
  title: "Primitives/Inputs/JimboInlineEdit",
  component: JimboInlineEdit,
};
export default meta;
type Story = StoryObj<typeof JimboInlineEdit>;

export const Default: Story = {
  render: () => {
    const [value, setValue] = useState("My filter name");
    return <JimboInlineEdit value={value} onChange={(e) => setValue(e.target.value)} />;
  },
};

export const Dim: Story = {
  render: () => <JimboInlineEdit defaultValue="Description text" dim tone="grey" size="sm" />,
};
