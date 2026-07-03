import type { Meta, StoryObj } from "@storybook/react-vite";
import { useEffect, useState } from "react";
import bootsharp, { Jimmolate } from "motely-wasm";
import { App as SeedFinderApp } from "../../../examples/seed-finder/src/App.js";
import { STARTER_JAML } from "../../../examples/seed-finder/src/constants.js";

Jimmolate.filter = () => 1;

function MotelyBootWrapper({ children }: { children: React.ReactNode }) {
  const [ready, setReady] = useState(false);
  useEffect(() => {
    bootsharp.boot().then(() => setReady(true));
  }, []);
  if (!ready) return <div style={{ color: "#c0caf5", padding: 16 }}>Booting Motely…</div>;
  return <>{children}</>;
}

const meta: Meta<typeof SeedFinderApp> = {
  title: "Seed Finder / SeedFinderApp",
  component: SeedFinderApp,
  parameters: {
    layout: "fullscreen",
  },
  decorators: [
    (Story) => (
      <MotelyBootWrapper>
        <Story />
      </MotelyBootWrapper>
    ),
  ],
};

export default meta;
type Story = StoryObj<typeof SeedFinderApp>;

export const Default: Story = {
  args: {
    initialJaml: STARTER_JAML,
  },
};
