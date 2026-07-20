import type { Meta, StoryObj } from "@storybook/react-vite";
import { useState } from "react";
import { Panel, Stack, Text } from "../components/layout.js";
import { JammyMascot, JammyOrbitalMenu } from "../components/mascot.js";

const meta: Meta<typeof JammyMascot> = {
  title: "Wire Format/Mascot",
  component: JammyMascot,
};

export default meta;

const menuItems = [
  { label: "Search", action: "search", tone: "red" as const },
  { label: "Analyze", action: "analyze", tone: "blue" as const },
  { label: "Filters", action: "filters", tone: "green" as const },
  { label: "Help", action: "help", tone: "gold" as const },
];

export const Idle: StoryObj<typeof JammyMascot> = {
  args: {
    mood: "idle",
    size: 96,
  },
};

function WithMenuStory() {
  const [lastAction, setLastAction] = useState<string | null>(null);
  return (
    <Stack gap={16} align="center">
      <Panel>
        <Text body={lastAction ? `Last action: ${lastAction}` : "Tap Jammy!"} variant="body" />
      </Panel>
      <JammyMascot
        mood="happy"
        size={96}
        menuItems={menuItems}
        onMenuAction={(action) => setLastAction(action)}
      />
    </Stack>
  );
}

export const WithMenu: StoryObj<typeof JammyMascot> = {
  render: () => <WithMenuStory />,
};

export const OrbitalMenuStandalone: StoryObj<typeof JammyOrbitalMenu> = {
  render: () => (
    <div style={{ position: "relative", width: 240, height: 240 }}>
      <div
        style={{
          position: "absolute",
          left: "50%",
          top: "50%",
          transform: "translate(-50%, -50%)",
          width: 64,
          height: 64,
          borderRadius: "50%",
          background: "var(--j-surface)",
          border: "2px solid var(--j-panel-edge)",
        }}
      />
      <JammyOrbitalMenu items={menuItems} radius={80} />
    </div>
  ),
};
