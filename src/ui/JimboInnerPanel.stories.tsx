import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboInnerPanel, JimboModal } from "./panel.js";
import { useState } from "react";
import { JimboButton } from "./JimboButton.js";

const meta: Meta = {
  title: "Primitives/Layout/JimboInnerPanel",
};
export default meta;

export const Default: StoryObj = {
  render: () => (
    <JimboInnerPanel style={{ width: 320 }}>
      A sunken inner panel — used to nest content inside a JimboPanel.
    </JimboInnerPanel>
  ),
};

export const Modal: StoryObj = {
  render: () => {
    const [open, setOpen] = useState(false);
    return (
      <div>
        <JimboButton onClick={() => setOpen(true)}>Open modal</JimboButton>
        <JimboModal open={open} onClose={() => setOpen(false)} title="Confirm">
          Are you sure you want to do this?
        </JimboModal>
      </div>
    );
  },
};
