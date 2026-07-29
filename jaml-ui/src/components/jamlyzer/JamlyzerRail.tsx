"use client";

import { JimboInnerPanel } from "../../ui/panel.js";
import { JimboText } from "../../ui/jimboText.js";
import { JimboButton } from "../../ui/JimboButton.js";
import { JimboStack } from "../../ui/JimboLayout.js";
import type { ParsedJamlClause } from "../../lib/jaml/parseClauses.js";

export interface JamlyzerRailProps {
  /** All parsed JAML clauses; the scoreboard lists them above the ante buttons. */
  clauses: ParsedJamlClause[];
  /** Per-should-clause tally values, in JAML order. */
  tallies?: number[] | Int32Array;
  anteNumbers: number[];
  availableAntes: Set<number>;
  selectedAnte: number;
  onSelectAnte: (ante: number) => void;
  onHoverClause: (clause: ParsedJamlClause | null) => void;
}

/** Left rail: clause scoreboard (hover to spotlight matches) + ante picker. */
export function JamlyzerRail({
  clauses,
  tallies,
  anteNumbers,
  availableAntes,
  selectedAnte,
  onSelectAnte,
  onHoverClause,
}: JamlyzerRailProps) {
  const shouldClauses = clauses.filter((c) => c.kind === "should");
  const otherClauses = clauses.filter((c) => c.kind !== "should");

  return (
    <JimboStack gap="sm" align="stretch" className="j-ante-scroll j-jamlyzer-view__rail">
      {clauses.length > 0 && (
        <JimboInnerPanel className="j-stack j-stack--gap-sm">
          <JimboText size="xs" tone="gold">
            Clauses
          </JimboText>
          {shouldClauses.map((clause, i) => (
            <JimboButton
              key={i}
              size="xs"
              tone="blue"
              fullWidth
              onMouseEnter={() => onHoverClause(clause)}
              onMouseLeave={() => onHoverClause(null)}
            >
              <JimboText size="micro" tone={tallies && i < tallies.length && tallies[i] > 0 ? "green" : "grey"}>
                {tallies && i < tallies.length ? `${tallies[i]} · ` : ""}
                {clause.label}
              </JimboText>
            </JimboButton>
          ))}
          {otherClauses.map((clause, i) => (
            <JimboButton
              key={`other-${i}`}
              size="xs"
              tone="blue"
              fullWidth
              onMouseEnter={() => onHoverClause(clause)}
              onMouseLeave={() => onHoverClause(null)}
            >
              <JimboText size="micro" tone={clause.kind === "must" ? "red" : "grey"}>
                {clause.kind === "must" ? "Must · " : "Not · "}
                {clause.label}
              </JimboText>
            </JimboButton>
          ))}
        </JimboInnerPanel>
      )}
      {anteNumbers.map((n) => (
        <JimboButton
          key={n}
          size="xs"
          tone="blue"
          fullWidth
          data-pressed={n === selectedAnte}
          disabled={!availableAntes.has(n)}
          onClick={() => onSelectAnte(n)}
          label={`Ante ${n}`}
        />
      ))}
    </JimboStack>
  );
}
