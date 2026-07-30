import type { Meta, StoryObj } from "@storybook/react-vite";
import { useCallback } from "react";
import { JimboCodeSurface } from "./JimboCodeSurface.js";
import { JimboText } from "./jimboText.js";

const meta: Meta<typeof JimboCodeSurface> = {
  title: "Primitives/Layout/JimboCodeSurface",
  component: JimboCodeSurface,
};
export default meta;
type Story = StoryObj<typeof JimboCodeSurface>;

/** Empty surface — what the caller sees before an editor view attaches. */
export const Default: Story = {
  render: () => <JimboCodeSurface minHeight={200} />,
};

/** The height floor is a prop, so a caller can size the well per screen. */
export const TallFloor: Story = {
  render: () => <JimboCodeSurface minHeight={420} />,
};

/** A mounted view owns everything inside; here a plain node stands in for one. */
export const WithMountedView: Story = {
  render: () => {
    const mount = useCallback((node: HTMLDivElement | null) => {
      if (!node) return;
      const line = document.createElement("pre");
      line.style.margin = "0";
      line.style.padding = "12px";
      line.style.fontFamily = "var(--j-font-code)";
      line.style.fontSize = "14px";
      line.style.color = "var(--j-green-text)";
      line.textContent = "must:\n  - joker: Blueprint\n    antes: [1, 2]";
      node.appendChild(line);
      return () => line.remove();
    }, []);
    return (
      <>
        <JimboText size="sm" tone="grey">
          Content below is appended by a ref callback, not by React.
        </JimboText>
        <JimboCodeSurface ref={mount} minHeight={160} />
      </>
    );
  },
};
