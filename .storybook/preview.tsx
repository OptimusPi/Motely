import type { Preview } from "@storybook/react-vite";
import "../src/ui/jimbo.css";
import "./preview.css";

const preview: Preview = {
  parameters: {
    layout: "fullscreen",
    backgrounds: {
      options: {
        balatro: { name: "balatro", value: "#0c1818" }
      }
    },
  },

  decorators: [
    (Story) => (
      <div className="story-root">
        <Story />
      </div>
    ),
  ],

  initialGlobals: {
    backgrounds: {
      value: "balatro"
    }
  }
};

export default preview;
