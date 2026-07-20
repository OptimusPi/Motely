import type { Meta, StoryObj } from "@storybook/react-vite";
import { JamlMapEditor } from "./JamlMapEditor.js";

const meta: Meta<typeof JamlMapEditor> = {
  title: "Screens/Ante Map/Editor",
  component: JamlMapEditor,
  parameters: { layout: "fullscreen" },
};

export default meta;
type Story = StoryObj<typeof JamlMapEditor>;

export const Default: Story = {
  args: {
    zone: "must",
  },
  decorators: [
    (Story) => (
      <div style={{ height: "100vh", background: "#1e2b2d" }}>
        <Story />
      </div>
    ),
  ],
};
