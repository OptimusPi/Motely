import type { Meta, StoryObj } from "@storybook/react-vite";
import { useState } from "react";
import { JimboTextArea } from "./JimboTextArea.js";

const meta: Meta<typeof JimboTextArea> = {
  title: "Primitives/Inputs/JimboTextArea",
  component: JimboTextArea,
};
export default meta;
type Story = StoryObj<typeof JimboTextArea>;

export const Default: Story = {
  render: () => {
    const [value, setValue] = useState("must:\n  - joker: Blueprint\n");
    return (
      <JimboTextArea
        value={value}
        onChange={(e) => setValue(e.target.value)}
        style={{ width: 360, minHeight: 200 }}
      />
    );
  },
};
