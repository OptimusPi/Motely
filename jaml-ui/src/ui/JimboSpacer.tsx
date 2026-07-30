"use client";

import type { HTMLAttributes } from "react";

export interface JimboSpacerProps extends HTMLAttributes<HTMLDivElement> {
  /** Vertical space in px. Default 16. */
  size?: number;
}

/** Empty vertical breathing room between stacks of content. */
export function JimboSpacer({ size = 16, style, ...rest }: JimboSpacerProps) {
  return <div aria-hidden style={{ ...style, height: size, width: "100%" }} {...rest} />;
}
