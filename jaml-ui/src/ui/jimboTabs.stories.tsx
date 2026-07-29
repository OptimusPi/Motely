import type { Meta, StoryObj } from "@storybook/react-vite";
import { useState } from "react";
import { JimboTabs } from "./jimboTabs.js";

const meta: Meta<typeof JimboTabs> = {
  title: "Primitives/Layout/JimboTabs",
  component: JimboTabs,
};
export default meta;
type Story = StoryObj<typeof JimboTabs>;

const TABS = [
  { id: "visual", label: "Visual" },
  { id: "code", label: "Code" },
  { id: "map", label: "Ante Map" },
];

export const Default: Story = {
  render: () => {
    const [active, setActive] = useState("visual");
    return <JimboTabs tabs={TABS} activeTab={active} onTabChange={setActive} />;
  },
};
