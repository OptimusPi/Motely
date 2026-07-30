"use client";

import type { CSSProperties, HTMLAttributes, Ref } from "react";

export type JimboCodeSurfaceProps = Omit<HTMLAttributes<HTMLDivElement>, "children"> & {
  ref?: Ref<HTMLDivElement>;
  /** Floor height in px for the surface before the view has content. */
  minHeight?: number;
};

/**
 * Mount point for an externally-managed editor view (CodeMirror, etc.).
 * Owns the sunken code background and the height floor; the view that attaches
 * to the ref owns everything inside it.
 */
export function JimboCodeSurface({
  className,
  minHeight = 320,
  style,
  ref,
  ...rest
}: JimboCodeSurfaceProps) {
  const classes = ["j-code-surface", className].filter(Boolean).join(" ");
  const vars = {
    "--j-code-surface-min-height": `${minHeight}px`,
    ...style,
  } as CSSProperties;
  return <div ref={ref} className={classes} style={vars} {...rest} />;
}
