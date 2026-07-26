import type { Preview } from "@storybook/react-vite";
import "../src/ui/jimbo.css";
import "./preview.css";
import { JimboBackground } from "../src/ui/JimboBackground.js";

const preview: Preview = {
  parameters: {
    layout: "fullscreen",
    options: {
      storySort: {
        order: ["Welcome", "Foundations", "Primitives", "Cards & Sprites", "Wire Format", "Screens"],
        method: "alphabetical",
      },
    },
    backgrounds: {
      options: {
        balatro: { name: "balatro", value: "#0c1818" }
      }
    },
  },

  decorators: [
    (Story) => (
      <div className="story-root">
        <JimboBackground />
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
