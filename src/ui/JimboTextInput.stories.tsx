import type { Meta, StoryObj } from "@storybook/react-vite";
import { useState } from "react";
import { JimboTextInput } from "./JimboTextInput.js";

const meta: Meta<typeof JimboTextInput> = {
  title: "Primitives/Inputs/JimboTextInput",
  component: JimboTextInput,
};
export default meta;
type Story = StoryObj<typeof JimboTextInput>;

export const Default: Story = {
  render: () => {
    const [value, setValue] = useState("");
    return (
      <JimboTextInput
        value={value}
        onChange={(e) => setValue(e.target.value)}
        placeholder="Aleeb"
        style={{ width: 200 }}
      />
    );
  },
};

export const Disabled: Story = {
  render: () => <JimboTextInput disabled placeholder="Disabled" style={{ width: 200 }} />,
};
