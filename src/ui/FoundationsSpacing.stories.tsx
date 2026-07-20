import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboText } from "./jimboText.js";

const meta: Meta = {
  title: "Foundations/Spacing & Radius",
};
export default meta;

const SPACES = ["--j-space-xs", "--j-space-sm", "--j-space-md", "--j-space-lg", "--j-space-xl"];
const RADII = ["--j-radius-sm", "--j-radius-md", "--j-radius-lg", "--j-radius-pill"];

export const Spacing: StoryObj = {
  render: () => (
    <div style={{ display: "grid", gap: 8 }}>
      {SPACES.map((token) => (
        <div
          key={token}
          style={{ display: "grid", gridTemplateColumns: "110px max-content", gap: 16, alignItems: "center" }}
        >
          <JimboText size="micro" tone="grey">
            {token}
          </JimboText>
          <div
            style={{
              width: `calc(var(${token}) * 10)`,
              height: 12,
              background: "var(--j-blue)",
              borderRadius: 2,
            }}
          />
        </div>
      ))}
      <JimboText size="micro" tone="grey">
        bars are 10x scale
      </JimboText>
    </div>
  ),
};

export const Radius: StoryObj = {
  render: () => (
    <div style={{ display: "grid", gridAutoFlow: "column", gridAutoColumns: "max-content", gap: 16 }}>
      {RADII.map((token) => (
        <div key={token} style={{ display: "grid", gap: 6, justifyItems: "center" }}>
          <div
            style={{
              width: 64,
              height: 48,
              background: "var(--j-dark-grey)",
              border: "2px solid var(--j-border-silver)",
              borderRadius: `var(${token})`,
            }}
          />
          <JimboText size="micro" tone="grey">
            {token}
          </JimboText>
        </div>
      ))}
    </div>
  ),
};
