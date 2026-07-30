import { useState, type CSSProperties } from "react";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboText } from "./jimboText.js";

const meta: Meta = {
  title: "Foundations/Spacing & Radius",
};
export default meta;

const SPACES = ["--j-space-xs", "--j-space-sm", "--j-space-md", "--j-space-lg", "--j-space-xl"];
const RADII = ["--j-radius-sm", "--j-radius-md", "--j-radius-lg", "--j-radius-pill"];

/* Token names and px values are code, not game copy — code font, no shadow. */
const LABEL: CSSProperties = {
  fontFamily: "var(--j-font-code)",
  fontSize: 11,
  lineHeight: 1.4,
  textShadow: "none",
  color: "var(--j-grey)",
};

/** Resolved value of a --j-* token, straight from the loaded stylesheet. */
function useTokenValue(token: string): string {
  const [value] = useState(() =>
    typeof document === "undefined"
      ? ""
      : getComputedStyle(document.documentElement).getPropertyValue(token).trim(),
  );
  return value;
}

/* The gap between the two tiles IS the token, at actual size — no scale trick. */
function SpacingRow({ token }: { token: string }) {
  const px = useTokenValue(token);
  return (
    <div style={{ display: "grid", gridTemplateColumns: "160px max-content", gap: 16, alignItems: "center" }}>
      <span style={LABEL}>
        {token.replace("--j-space-", "")} · {px}
      </span>
      <div style={{ display: "grid", gridAutoFlow: "column", gridAutoColumns: "max-content", gap: `var(${token})` }}>
        <div style={{ width: 24, height: 24, background: "var(--j-blue)", borderRadius: "var(--j-radius-sm)" }} />
        <div style={{ width: 24, height: 24, background: "var(--j-blue)", borderRadius: "var(--j-radius-sm)" }} />
      </div>
    </div>
  );
}

function RadiusTile({ token }: { token: string }) {
  const px = useTokenValue(token);
  return (
    <div style={{ display: "grid", gap: 6, justifyItems: "center" }}>
      <div
        style={{
          width: 64,
          height: 48,
          background: "var(--j-dark-grey)",
          border: "2px solid var(--j-border-silver)",
          borderRadius: `var(${token})`,
        }}
      />
      <span style={LABEL}>
        {token.replace("--j-radius-", "")} · {px}
      </span>
    </div>
  );
}

export const Tokens: StoryObj = {
  render: () => (
    <div style={{ display: "grid", gap: 24 }}>
      <div style={{ display: "grid", gap: 8 }}>
        <JimboText size="lg" tone="grey">
          spacing
        </JimboText>
        {SPACES.map((token) => (
          <SpacingRow key={token} token={token} />
        ))}
      </div>
      <div style={{ display: "grid", gap: 8 }}>
        <JimboText size="lg" tone="grey">
          radius
        </JimboText>
        <div style={{ display: "grid", gridAutoFlow: "column", gridAutoColumns: "max-content", gap: 16 }}>
          {RADII.map((token) => (
            <RadiusTile key={token} token={token} />
          ))}
        </div>
      </div>
    </div>
  ),
};
