import type { Meta, StoryObj } from "@storybook/react-vite";
import { useState } from "react";
import { JimboOuterTab } from "./JimboOuterTab";

const meta: Meta<typeof JimboOuterTab> = {
  title: "ui/JimboOuterTab",
  component: JimboOuterTab,
};
export default meta;
type Story = StoryObj<typeof JimboOuterTab>;

export const Default: Story = {
  args: { label: "Filter", tone: "blue", onToggleFullscreen: () => {} },
};

export const Tones: Story = {
  render: () => (
    <div style={{ display: "grid", gap: 8, width: 320 }}>
      <JimboOuterTab label="Filter" tone="blue" />
      <JimboOuterTab label="Search" tone="green" />
      <JimboOuterTab label="Results" tone="gold" />
      <JimboOuterTab label="Jamlyze" tone="purple" />
    </div>
  ),
};

export const DraggableSwap: Story = {
  render: () => {
    const [order, setOrder] = useState(["Filter", "Search", "Results", "Jamlyze"]);
    const [over, setOver] = useState<string | null>(null);
    return (
      <div style={{ display: "grid", gap: 8, width: 320 }}>
        {order.map((name) => (
          <JimboOuterTab
            key={name}
            label={name}
            tone={name === over ? "gold" : "blue"}
            draggable
            onDragStart={(e) => e.dataTransfer.setData("text/plain", name)}
            onDragOver={(e) => { e.preventDefault(); setOver(name); }}
            onDragLeave={() => setOver((v) => (v === name ? null : v))}
            onDrop={(e) => {
              e.preventDefault();
              const from = e.dataTransfer.getData("text/plain");
              setOver(null);
              setOrder((prev) => {
                const next = [...prev];
                const a = next.indexOf(from), b = next.indexOf(name);
                if (a === -1 || b === -1) return prev;
                [next[a], next[b]] = [next[b], next[a]];
                return next;
              });
            }}
          />
        ))}
      </div>
    );
  },
};

export const FullscreenToggle: Story = {
  render: () => {
    const [fs, setFs] = useState(false);
    return (
      <div style={{ width: 320 }}>
        <JimboOuterTab label="Jamlyze" tone="purple" fullscreen={fs} onToggleFullscreen={() => setFs((v) => !v)} />
      </div>
    );
  },
};
