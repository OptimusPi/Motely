import type { Meta, StoryObj } from "@storybook/react-vite";
import { useState } from "react";
import { JimboSpinner } from "./JimboSpinner.js";

const meta: Meta<typeof JimboSpinner> = {
  title: "Primitives/Display/JimboSpinner",
  component: JimboSpinner,
};
export default meta;
type Story = StoryObj<typeof JimboSpinner>;

const DECKS = ["Red", "Blue", "Yellow", "Green", "Black"];

export const Default: Story = {
  render: () => {
    const [i, setI] = useState(0);
    return (
      <JimboSpinner
        label="Deck"
        value={DECKS[i]}
        onPrev={() => setI((n) => (n - 1 + DECKS.length) % DECKS.length)}
        onNext={() => setI((n) => (n + 1) % DECKS.length)}
      />
    );
  },
};
