import type { Meta, StoryObj } from "@storybook/react-vite";
import { useEffect, useState } from "react";
import bootsharp from "motely-wasm";
import { bindJimmolateBridge } from "jaml-codemirror";
import { McpSeedFinderApp } from "../../components/McpSeedFinderApp.js";

bindJimmolateBridge();

const STARTER_JAML = `must:
  - joker: Blueprint
    antes: [1, 2, 3, 4, 5, 6, 7, 8]
deck: Red
stake: White
`;

function MotelyBootWrapper({ children }: { children: React.ReactNode }) {
  const [ready, setReady] = useState(false);
  useEffect(() => {
    bootsharp.boot().then(() => setReady(true));
  }, []);
  if (!ready) return <div style={{ color: "#c0caf5", padding: 16 }}>Booting Motely…</div>;
  return <>{children}</>;
}

function McpSeedFinderStory() {
  const [jaml, setJaml] = useState(STARTER_JAML);
  return <McpSeedFinderApp jaml={jaml} onChange={setJaml} />;
}

const meta: Meta = {
  title: "Seed Finder / McpSeedFinderApp",
  parameters: { layout: "fullscreen" },
  decorators: [
    (Story) => (
      <MotelyBootWrapper>
        <Story />
      </MotelyBootWrapper>
    ),
  ],
};

export default meta;
type Story = StoryObj;

export const Default: Story = {
  render: () => <McpSeedFinderStory />,
};
