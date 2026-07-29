"use client";

import { useMemo, useState } from "react";
import type { MotelyJamlyzerSeedResult, MotelyJamlyzerAnteResult } from "motely-wasm";
import { JamlyzerView } from "./JamlyzerView.js";
import { decodeMotelyItem } from "../decode/motelyItemDecoder.js";
import { JimboPanel } from "../ui/JimboPanel.js";
import { JimboInnerPanel } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { JimboBadge } from "../ui/JimboBadge.js";
import { JimboButton } from "../ui/JimboButton.js";
import { JimboRow } from "../ui/JimboLayout.js";
import { JimboSeedCopyChip } from "../ui/JimboSeedCopyChip.js";
import {
  parseJamlClauses,
  type ParsedJamlClause,
  matchMotelyItemToClause,
  matchClauseToAnte,
} from "../lib/jaml/parseClauses.js";

export interface JamlyzerBulkProps {
  results: MotelyJamlyzerSeedResult[];
  /** Raw JAML text; used to derive clause identities if `clauses` is not provided. */
  jamlText?: string;
  /** Pre-parsed clauses (alternative to `jamlText`). */
  clauses?: ParsedJamlClause[];
  /** Per-seed per-should-clause tally values, in JAML order. */
  tallies?: (number[] | Int32Array)[];
  /** Optional deck/stake applied to every seed in the bulk view. */
  deck?: number;
  stake?: number;
}

function pullItems(ante: MotelyJamlyzerAnteResult): MotelyJamlyzerAnteResult["pulls"]["judgementJokers"] {
  return [
    ...ante.pulls.judgementJokers,
    ...ante.pulls.wraithJokers,
    ...ante.pulls.emperorTarots,
    ...ante.pulls.purpleSealTarots,
    ...ante.pulls.sixthSenseSpectrals,
    ...ante.pulls.seanceSpectrals,
    ...ante.pulls.riffRaffJokers,
    ...ante.pulls.rareTagJokers,
    ...ante.pulls.uncommonTagJokers,
    ...ante.pulls.legendaryJokers,
  ];
}

function seedClauseMatches(
  seedResult: MotelyJamlyzerSeedResult,
  clauses: ParsedJamlClause[]
): Map<ParsedJamlClause, number[]> {
  const map = new Map<ParsedJamlClause, number[]>();
  for (const clause of clauses) {
    const antes: number[] = [];
    for (const ante of seedResult.antes) {
      if (!matchClauseToAnte(clause, ante.ante)) continue;
      const allItems = [
        ...ante.shopItems,
        ...ante.packs.flatMap((p) => p.items),
        ...pullItems(ante),
      ];
      const matched = allItems.some((item) => matchMotelyItemToClause(decodeMotelyItem(item) ?? {}, clause));
      if (matched) antes.push(ante.ante);
    }
    map.set(clause, antes);
  }
  return map;
}

export function ClauseHitPanel({
  clause,
  hitAntes,
  tally,
}: {
  clause: ParsedJamlClause;
  hitAntes: number[];
  tally?: number;
}) {
  const label =
    clause.kind === "must" ? `Must · ${clause.label}` : clause.kind === "mustNot" ? `Not · ${clause.label}` : clause.label;
  const labelTone = clause.kind === "must" ? "red" : "grey";
  const badgeTone = clause.kind === "must" ? "red" : clause.kind === "mustNot" ? "grey" : "green";
  return (
    <JimboInnerPanel className="j-stack j-stack--gap-xs">
      <JimboText size="xs" tone={labelTone}>
        {label}
        {tally !== undefined && <JimboText size="xs" tone="green"> ({tally})</JimboText>}
      </JimboText>
      <JimboRow wrap gap="xs" align="center">
        {hitAntes.length > 0 ? (
          hitAntes.map((n) => (
            <JimboBadge key={n} tone={badgeTone} size="sm">
              Ante {n}
            </JimboBadge>
          ))
        ) : (
          <JimboText size="micro" tone="grey">
            no hits
          </JimboText>
        )}
      </JimboRow>
    </JimboInnerPanel>
  );
}

export function JamlyzerBulk({ results, jamlText, clauses: clausesProp, tallies, deck, stake }: JamlyzerBulkProps) {
  const [expandedSeed, setExpandedSeed] = useState<string | null>(null);

  const clauses = useMemo(() => {
    if (clausesProp) return clausesProp;
    if (jamlText) return parseJamlClauses(jamlText).all;
    return [];
  }, [clausesProp, jamlText]);

  const shouldClauses = useMemo(() => clauses.filter((c) => c.kind === "should"), [clauses]);
  const otherClauses = useMemo(() => clauses.filter((c) => c.kind !== "should"), [clauses]);

  if (results.length === 0) {
    return (
      <JimboPanel body>
        <JimboText tone="grey">No seeds to analyze.</JimboText>
      </JimboPanel>
    );
  }

  return (
    <JimboPanel title="Bulk seed analysis" tone="gold">
      <JimboText tone="grey">
        {results.length} seed{results.length === 1 ? "" : "s"} analyzed
      </JimboText>

      {results.map((result, index) => {
        const matches = seedClauseMatches(result, clauses);
        const isExpanded = expandedSeed === result.seed;
        const seedTallies = tallies && index < tallies.length ? tallies[index] : undefined;

        return (
          <JimboPanel key={result.seed} body>
            <JimboRow wrap gap="md" align="center">
              <JimboSeedCopyChip value={result.seed} />
              <JimboText tone="grey">
                Score: <JimboText tone="gold">{result.score}</JimboText>
              </JimboText>
              <JimboButton
                size="xs"
                tone="blue"
                onClick={() => setExpandedSeed(isExpanded ? null : result.seed)}
                label={isExpanded ? "Collapse" : "Expand"}
              />
            </JimboRow>

            {shouldClauses.length > 0 && (
              <JimboRow wrap gap="md" align="start">
                {shouldClauses.map((clause, i) => {
                  const tally = seedTallies && i < seedTallies.length ? seedTallies[i] : undefined;
                  return (
                    <ClauseHitPanel
                      key={i}
                      clause={clause}
                      hitAntes={matches.get(clause) ?? []}
                      tally={tally}
                    />
                  );
                })}
              </JimboRow>
            )}

            {otherClauses.length > 0 && (
              <JimboRow wrap gap="md" align="start">
                {otherClauses.map((clause, i) => (
                  <ClauseHitPanel
                    key={`other-${i}`}
                    clause={clause}
                    hitAntes={matches.get(clause) ?? []}
                  />
                ))}
              </JimboRow>
            )}

            {isExpanded && (
              <JamlyzerView
                result={result}
                deck={deck}
                stake={stake}
                clauses={clauses}
                tallies={seedTallies ? [...seedTallies] : undefined}
              />
            )}
          </JimboPanel>
        );
      })}
    </JimboPanel>
  );
}
