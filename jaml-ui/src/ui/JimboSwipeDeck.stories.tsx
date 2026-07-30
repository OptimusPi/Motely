import { useState } from "react";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboSwipeDeck, type JimboSwipeDirection } from "./JimboSwipeDeck.js";
import { JimboText } from "./jimboText.js";

const meta: Meta = {
  title: "Primitives/Interaction/JimboSwipeDeck",
};
export default meta;

const SEEDS = ["aleeb88", "7nkb123", "qq4x2zp", "trib0lt", "88888888"];

function SeedCard({ seed }: { seed: string }) {
  return (
    <div
      style={{
        position: "absolute",
        inset: 0,
        display: "grid",
        placeItems: "center",
        background: "var(--j-surface-inset)",
        border: "var(--j-panel-border-width) solid var(--j-border-silver)",
        borderRadius: "var(--j-radius-lg)",
      }}
    >
      <JimboText size="lg" tone="white">
        {seed}
      </JimboText>
    </div>
  );
}

export const Default: StoryObj = {
  render: () => (
    <JimboSwipeDeck>
      {SEEDS.map((seed) => (
        <SeedCard key={seed} seed={seed} />
      ))}
    </JimboSwipeDeck>
  ),
};

export const OneCard: StoryObj = {
  render: () => (
    <JimboSwipeDeck>
      <SeedCard seed="lastone" />
    </JimboSwipeDeck>
  ),
};

export const Empty: StoryObj = {
  render: () => <JimboSwipeDeck />,
};

function DecisionLogDemo() {
  const [log, setLog] = useState<string[]>([]);
  return (
    <div style={{ display: "grid", gap: 12, justifyItems: "center" }}>
      <JimboSwipeDeck
        height={300}
        onDecide={(index, direction: JimboSwipeDirection) =>
          setLog((entries) => [...entries, `card ${index + 1}: ${direction}`])
        }
      >
        {SEEDS.map((seed) => (
          <SeedCard key={seed} seed={seed} />
        ))}
      </JimboSwipeDeck>
      <div style={{ display: "grid", gap: 2, justifyItems: "center" }}>
        {log.length === 0 ? (
          <JimboText size="xs" tone="grey">
            Swipe, use the buttons, or arrow keys — decisions land here
          </JimboText>
        ) : (
          log.map((entry, i) => (
            <JimboText key={i} size="xs" tone="white">
              {entry}
            </JimboText>
          ))
        )}
      </div>
    </div>
  );
}

export const DecisionLog: StoryObj = {
  render: () => <DecisionLogDemo />,
};
