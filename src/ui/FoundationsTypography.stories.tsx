import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboText } from "./jimboText.js";

const meta: Meta = {
  title: "Foundations/Typography",
};
export default meta;

/** The .j-text size classes with their px values from jimbo.css. */
const SCALE = [
  ["j-text--display", "display 32px"],
  ["j-text--xl", "xl 24px"],
  ["j-text--lg", "lg 18px"],
  ["j-text--heading", "heading 16px"],
  ["j-text--md", "md 16px"],
  ["j-text--sm", "sm 14px"],
  ["j-text--body", "body 13px"],
  ["j-text--xs", "xs 12px"],
  ["j-text--label", "label 10px"],
] as const;

export const Scale: StoryObj = {
  render: () => (
    <div style={{ display: "grid", gap: 10, alignItems: "baseline" }}>
      {SCALE.map(([cls, label]) => (
        <div
          key={cls}
          style={{ display: "grid", gridTemplateColumns: "110px max-content", gap: 16, alignItems: "baseline" }}
        >
          <JimboText size="micro" tone="grey">
            {label}
          </JimboText>
          <span className={`j-text ${cls}`}>Chips and Mult</span>
        </div>
      ))}
    </div>
  ),
};

export const Fonts: StoryObj = {
  render: () => (
    <div style={{ display: "grid", gap: 14 }}>
      {(
        [
          ["--j-font", "m6x11plus at 18 (native grid 18/36/54)", 18],
          ["--j-font-m6x11", "m6x11 at 16 (native grid 16/32/48)", 16],
          ["--j-font-code", "code font for the JAML IDE", 13],
        ] as const
      ).map(([token, label, px]) => (
        <div key={token} style={{ display: "grid", gap: 2 }}>
          <JimboText size="micro" tone="grey">
            {label}
          </JimboText>
          <span
            className="j-text"
            style={{ fontFamily: `var(${token})`, fontSize: px }}
          >
            Triboulet appears in ante 1 — score 100
          </span>
        </div>
      ))}
    </div>
  ),
};
