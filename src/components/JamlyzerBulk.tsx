"use client";

import React, { useMemo, useState } from "react";
import type { MotelyJamlyzerSeedResult, MotelyJamlyzerAnteResult } from "motely-wasm";
import { JamlyzerView } from "./JamlyzerView.js";
import { decodeMotelyItem } from "../decode/motelyItemDecoder.js";
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

export function JamlyzerBulk({ results, jamlText, clauses: clausesProp, tallies, deck, stake }: JamlyzerBulkProps) {
  const [expandedSeed, setExpandedSeed] = useState<string | null>(null);

  const clauses = useMemo(() => {
    if (clausesProp) return clausesProp;
    if (jamlText) return parseJamlClauses(jamlText).all;
    return [];
  }, [clausesProp, jamlText]);

  const shouldClauses = useMemo(() => clauses.filter((c) => c.kind === "should"), [clauses]);

  if (results.length === 0) {
    return (
      <div className="j-panel j-text j-text--grey">
        No seeds to analyze.
      </div>
    );
  }

  return (
    <div className="j-panel" style={{ gap: "var(--j-space-lg)" }}>
      <div className="j-text j-text--xl j-text--gold">Bulk Seed Analysis</div>
      <div className="j-text j-text--body j-text--grey">
        {results.length} seed{results.length === 1 ? "" : "s"} analyzed
      </div>

      {results.map((result, index) => {
        const matches = seedClauseMatches(result, clauses);
        const isExpanded = expandedSeed === result.seed;
        const seedTallies = tallies && index < tallies.length ? tallies[index] : undefined;

        return (
          <div key={result.seed} className="j-panel" style={{ gap: "var(--j-space-md)" }}>
            <div className="j-flex" style={{ alignItems: "center", gap: "var(--j-space-md)", flexWrap: "wrap" }}>
              <div className="j-text j-text--lg j-text--white">{result.seed}</div>
              <div className="j-badge j-badge--gold j-badge--md">Score: {result.score}</div>
              <button
                className="j-btn j-btn--xs"
                onClick={() => setExpandedSeed(isExpanded ? null : result.seed)}
              >
                <span className="j-btn__face j-text j-text--micro">
                  {isExpanded ? "Collapse" : "Expand"}
                </span>
              </button>
            </div>

            {shouldClauses.length > 0 && (
              <div className="j-flex" style={{ gap: "var(--j-space-md)", flexWrap: "wrap" }}>
                {shouldClauses.map((clause, i) => {
                  const hitAntes = matches.get(clause) ?? [];
                  const tally = seedTallies && i < seedTallies.length ? seedTallies[i] : undefined;
                  return (
                    <div key={i} className="j-inner-panel" style={{ minWidth: 140 }}>
                      <div className="j-text j-text--label j-text--grey" style={{ marginBottom: "var(--j-space-sm)" }}>
                        {clause.label}
                        {tally !== undefined && <span className="j-text--green"> ({tally})</span>}
                      </div>
                      <div className="j-flex" style={{ gap: "var(--j-space-xs)", flexWrap: "wrap" }}>
                        {hitAntes.length > 0 ? (
                          hitAntes.map((n) => (
                            <span key={n} className="j-badge j-badge--green j-badge--sm">
                              Ante {n}
                            </span>
                          ))
                        ) : (
                          <span className="j-text j-text--micro j-text--grey">no hits</span>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            )}

            {clauses.filter((c) => c.kind !== "should").length > 0 && (
              <div className="j-flex" style={{ gap: "var(--j-space-md)", flexWrap: "wrap" }}>
                {clauses
                  .filter((c) => c.kind !== "should")
                  .map((clause, i) => {
                    const hitAntes = matches.get(clause) ?? [];
                    const color = clause.kind === "must" ? "red" : "grey";
                    return (
                      <div key={`other-${i}`} className="j-inner-panel" style={{ minWidth: 140 }}>
                        <div className={`j-text j-text--label j-text--${clause.kind === "must" ? "red" : "grey"}`} style={{ marginBottom: "var(--j-space-sm)" }}>
                          {clause.kind === "must" ? "MUST " : "NOT "}{clause.label}
                        </div>
                        <div className="j-flex" style={{ gap: "var(--j-space-xs)", flexWrap: "wrap" }}>
                          {hitAntes.length > 0 ? (
                            hitAntes.map((n) => (
                              <span key={n} className={`j-badge j-badge--${color} j-badge--sm`}>
                                Ante {n}
                              </span>
                            ))
                          ) : (
                            <span className="j-text j-text--micro j-text--grey">no hits</span>
                          )}
                        </div>
                      </div>
                    );
                  })}
              </div>
            )}

            {isExpanded && (
              <div style={{ marginTop: "var(--j-space-md)" }}>
                <JamlyzerView
                  result={result}
                  deck={deck}
                  stake={stake}
                  clauses={clauses}
                  tallies={seedTallies ? [...seedTallies] : undefined}
                />
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
