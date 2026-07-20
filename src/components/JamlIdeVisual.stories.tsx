import { useState } from "react";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { JamlIdeVisual, type JamlVisualFilter } from "./JamlIdeVisual.js";

const STARTER_FILTER: JamlVisualFilter = {
  name: "Demo Filter",
  must: [
    { id: "1", type: "joker", value: "Blueprint", label: "Blueprint", antes: [1, 2, 3] },
    { id: "2", type: "voucher", value: "Telescope", label: "Telescope", antes: [1] },
  ],
  should: [
    { id: "3", type: "joker", value: "Brainstorm", label: "Brainstorm", antes: [2, 3, 4], score: 5 },
  ],
  mustnot: [
    { id: "4", type: "tag", value: "Egg", label: "Egg Tag", antes: [1, 2] },
  ],
};

function JamlIdeVisualStory() {
  const [filter, setFilter] = useState(STARTER_FILTER);
  return <JamlIdeVisual filter={filter} onChange={setFilter} />;
}

const meta: Meta = {
  title: "Screens/JAML IDE/Visual Builder",
  parameters: { layout: "fullscreen" },
};

export default meta;
type Story = StoryObj;

export const Default: Story = {
  render: () => <JamlIdeVisualStory />,
  decorators: [
    (Story) => (
      <div style={{ height: "100vh", background: "#1e2b2d" }}>
        <Story />
      </div>
    ),
  ],
};
