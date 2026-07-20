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

function Swatch({ token }: { token: string }) {
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
      <JimboText size="micro" tone="grey">
        {token}
      </JimboText>
    </div>
  );
}

export const Tokens: StoryObj = {
  render: () => (
    <div style={{ display: "grid", gap: 20 }}>
      {GROUPS.map(({ name, tokens }) => (
        <div key={name} style={{ display: "grid", gap: 8 }}>
          <JimboText size="sm" tone="grey">
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
