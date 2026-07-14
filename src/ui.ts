"use client";

export * from "./ui/tokens.js";
export { JimboButton, type JimboButtonProps } from "./ui/JimboButton.js";
export { JimboTextArea, type JimboTextAreaProps } from "./ui/JimboTextArea.js";
export { JimboPanel, type JimboPanelProps } from "./ui/JimboPanel.js";
export { JimboBackground } from "./ui/JimboBackground.js";
export { JimboApp, type JimboAppProps, type JimboAppVariant } from "./ui/JimboApp.js";
export { JimboInset, type JimboInsetProps } from "./ui/JimboInset.js";
export { JimboWordmark, type JimboWordmarkProps } from "./ui/JimboWordmark.js";
export {
  JimboSectionHeader,
  type JimboSectionHeaderProps,
  type JimboSectionTone,
} from "./ui/JimboSectionHeader.js";
export {
  JimboText,
  type JimboTextProps,
  type JimboTextSize,
  type JimboTextTone,
} from "./ui/jimboText.js";

// Side-effect: design system CSS custom properties + component classes
import "./ui/jimbo.css";
