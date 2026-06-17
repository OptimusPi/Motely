"use client";

import { ReactNode } from "react";
import { JimboApp, JimboBackground } from "jaml-ui/ui";

/**
 * JamlSeedLabLayout — Shared layout for all JAML Seed Lab pages.
 *
 * Wraps pages with JimboBackground + JimboApp design system.
 * Consumers can override or compose with their own layout.
 */

export function JamlSeedLabLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <JimboBackground />
      <JimboApp>{children}</JimboApp>
    </>
  );
}
