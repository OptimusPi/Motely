import type { Meta, StoryObj } from "@storybook/react-vite";
import { useState } from "react";
import { JimboButton, JimboTextArea } from "../../ui.js";
import { Stack } from "../components/layout.js";

const meta: Meta = {
  title: "ui/Primitives",
};

export default meta;

export const Buttons: StoryObj = {
  render: () => (
    <Stack gap={12}>
      <JimboButton tone="orange">Search Random</JimboButton>
      <JimboButton tone="blue" size="sm">
        Load
      </JimboButton>
      <JimboButton tone="red" size="xs" disabled>
        Disabled
      </JimboButton>
    </Stack>
  ),
};

export const TextArea: StoryObj = {
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
