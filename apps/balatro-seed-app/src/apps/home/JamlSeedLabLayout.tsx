"use client";

import { ReactNode } from "react";
import { JimboBackground } from "jaml-ui/ui";
import { JimboBalatroFooter } from "jaml-ui/ui";

/**
 * JamlSeedLabLayout — Shared layout for all JAML Seed Lab pages.
 *
 * Wraps pages with JimboBackground + JimboBalatroFooter.
 * Consumers can override or compose with their own layout.
 */

export function JamlSeedLabLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <JimboBackground />
      {children}
      <JimboBalatroFooter />
    </>
  );
}
