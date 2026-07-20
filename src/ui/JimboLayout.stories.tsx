import type { Meta, StoryObj } from "@storybook/react-vite";
import type { ReactNode } from "react";
import { JimboRow, JimboStack, type JimboGap } from "./JimboLayout.js";
import { JimboText } from "./jimboText.js";

const meta: Meta = {
  title: "Primitives/Layout/JimboLayout",
};
export default meta;

/** Visible placeholder so gaps and alignment read at a glance. */
function Tile({ children, tall }: { children: ReactNode; tall?: boolean }) {
  return (
    <div
      style={{
        display: "grid",
        placeItems: "center",
        padding: tall ? "18px 14px" : "6px 14px",
        background: "var(--j-surface-inset)",
        border: "1px solid var(--j-border-south)",
        borderRadius: "var(--j-radius-md)",
      }}
    >
      <JimboText size="sm" tone="white">
        {children}
      </JimboText>
    </div>
  );
}

const GAPS: JimboGap[] = ["xs", "sm", "md", "lg", "xl"];

export const Stack: StoryObj = {
  render: () => (
    <JimboStack gap="md" style={{ width: 260 }}>
      <Tile>first</Tile>
      <Tile>second</Tile>
      <Tile>third</Tile>
    </JimboStack>
  ),
};

export const StackGaps: StoryObj = {
  render: () => (
    <JimboRow gap="xl" align="start">
      {GAPS.map((gap) => (
        <JimboStack key={gap} gap={gap}>
          <JimboText size="xs" tone="grey">
            gap {gap}
          </JimboText>
          <Tile>a</Tile>
          <Tile>b</Tile>
          <Tile>c</Tile>
        </JimboStack>
      ))}
    </JimboRow>
  ),
};

export const Row: StoryObj = {
  render: () => (
    <JimboRow gap="md">
      <Tile>one</Tile>
      <Tile tall>two, taller</Tile>
      <Tile>three</Tile>
    </JimboRow>
  ),
};

export const RowAlign: StoryObj = {
  render: () => (
    <JimboStack gap="lg">
      {(["start", "center", "end"] as const).map((align) => (
        <JimboStack key={align} gap="xs">
          <JimboText size="xs" tone="grey">
            align {align}
          </JimboText>
          <JimboRow gap="md" align={align}>
            <Tile>short</Tile>
            <Tile tall>tall neighbor</Tile>
            <Tile>short</Tile>
          </JimboRow>
        </JimboStack>
      ))}
    </JimboStack>
  ),
};

export const RowWrap: StoryObj = {
  render: () => (
    <div style={{ width: 300 }}>
      <JimboRow gap="sm" wrap>
        {Array.from({ length: 10 }, (_, i) => (
          <Tile key={i}>chip {i + 1}</Tile>
        ))}
      </JimboRow>
    </div>
  ),
};

export const Composed: StoryObj = {
  render: () => (
    <JimboStack gap="lg" style={{ width: 340 }}>
      <JimboRow gap="md" justify="between" style={{ gridAutoColumns: "1fr max-content" }}>
        <Tile>header</Tile>
        <Tile>actions</Tile>
      </JimboRow>
      <JimboRow gap="md" align="stretch">
        <Tile tall>sidebar</Tile>
        <Tile tall>content</Tile>
      </JimboRow>
      <Tile>footer</Tile>
    </JimboStack>
  ),
};
