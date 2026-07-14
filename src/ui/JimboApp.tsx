"use client";

import type { HTMLAttributes, ReactNode } from "react";

export type JimboAppVariant = "embed" | "page";

export interface JimboAppProps extends HTMLAttributes<HTMLDivElement> {
  /** embed = fixed 320×540 MCP shell; page = fluid full-viewport web shell */
  variant?: JimboAppVariant;
  /** Main column. For embed, scrolls inside the fixed frame when scroll is true. */
  scroll?: boolean;
  footer?: ReactNode;
}

/**
 * App chrome. Embed = Balatro-toy / MCP rectangle. Page = fan-site / seedfinder shell.
 * Both set the Jimbo font stack and color; neither paints a second background
 * (use JimboBackground behind).
 */
export function JimboApp({
  variant = "embed",
  scroll = true,
  footer,
  className,
  children,
  ...rest
}: JimboAppProps) {
  if (variant === "page") {
    const classes = ["j-page", className].filter(Boolean).join(" ");
    return (
      <div className={classes} {...rest}>
        <div className="j-page__main">{children}</div>
        {footer ? <div className="j-page__footer">{footer}</div> : null}
      </div>
    );
  }

  const classes = ["j-app", className].filter(Boolean).join(" ");
  const bodyClass = scroll ? "j-app__scroll" : "j-app__content";
  return (
    <div className={classes} {...rest}>
      <div className={bodyClass}>{children}</div>
      {footer ? <div className="j-app__footer">{footer}</div> : null}
    </div>
  );
}
