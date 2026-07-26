import { useState, type CSSProperties } from "react";
import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboText } from "./jimboText.js";

const meta: Meta = {
  title: "Foundations/Colors",
};
export default meta;

/** Token names, grouped the way jimbo-tokens.css groups them. */
const GROUPS: Array<{ name: string; tokens: string[] }> = [
  { name: "game", tokens: ["--j-red", "--j-blue", "--j-green", "--j-orange", "--j-gold", "--j-purple"] },
  { name: "pressed", tokens: ["--j-dark-red", "--j-dark-blue", "--j-dark-green", "--j-dark-orange", "--j-grey", "--j-dark-grey"] },
  { name: "surfaces", tokens: ["--j-darkest", "--j-surface-inset"] },
  { name: "chrome", tokens: ["--j-border-silver", "--j-border-south", "--j-white", "--j-orange-text"] },
];

/* Token names and hex values are code, not game copy — the pixel font at 10px
   turns them to mush and the .j-text shadow smears underneath. Code font,
   no shadow. */
const LABEL: CSSProperties = {
  fontFamily: "var(--j-font-code)",
  fontSize: 11,
  lineHeight: 1.4,
  textShadow: "none",
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

function Swatch({ token }: { token: string }) {
  const hex = useTokenValue(token);
  return (
    <div style={{ display: "grid", gap: 4, justifyItems: "center" }}>
      <div
        style={{
          width: 72,
          height: 48,
          background: `var(${token})`,
          borderRadius: "var(--j-radius-md)",
          border: "1px solid var(--j-border-south)",
        }}
      />
      <span style={{ ...LABEL, color: "var(--j-white)" }}>{token.replace("--j-", "")}</span>
      <span style={{ ...LABEL, color: "var(--j-grey)" }}>{hex}</span>
    </div>
  );
}

export const Tokens: StoryObj = {
  render: () => (
    <div style={{ display: "grid", gap: 24 }}>
      {GROUPS.map(({ name, tokens }) => (
        <div key={name} style={{ display: "grid", gap: 8 }}>
          <JimboText size="lg" tone="grey">
            {name}
          </JimboText>
          <div style={{ display: "grid", gridAutoFlow: "column", gridAutoColumns: "max-content", gap: 12 }}>
            {tokens.map((token) => (
              <Swatch key={token} token={token} />
            ))}
          </div>
        </div>
      ))}
    </div>
  ),
};
