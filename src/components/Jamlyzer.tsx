"use client";

import React, { useEffect, useMemo, useState } from "react";
import { Motely } from "motely-wasm";
import type { MotelyJamlyzerResult, MotelyJamlyzerSeedResult } from "motely-wasm/motely/analysis";
import { ensureMotelyReady } from "../lib/motely/runtime.js";
import { JimboInnerPanel, JimboPanel } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { JamlSeedSpinner } from "./JamlSeedSpinner.js";

export interface JamlyzerProps {
  /** Full JAML document. Seeds come from the top-level `seeds:` array via Motely. */
  jaml: string;
  className?: string;
  style?: React.CSSProperties;
}

type JamlyzerLoadState =
  | { status: "loading" }
  | { status: "ready"; result: MotelyJamlyzerResult; elapsedMs: number }
  | { status: "error"; message: string };

function seedMatches(row: MotelyJamlyzerSeedResult): boolean {
  return (row.score ?? 0) >= 1;
}

export function Jamlyzer({ jaml, className = "", style }: JamlyzerProps) {
  const [load, setLoad] = useState<JamlyzerLoadState>({ status: "loading" });
  const [index, setIndex] = useState(0);
  const [lastJaml, setLastJaml] = useState(jaml);

  // Reset to loading the moment `jaml` changes — render-phase derivation
  // (React's "Adjusting state when a prop changes" pattern) avoids the
  // cascading render that synchronous setState-in-effect would cause.
  if (jaml !== lastJaml) {
    setLastJaml(jaml);
    setLoad({ status: "loading" });
    setIndex(0);
  }

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      try {
        await ensureMotelyReady();
        const trimmed = jaml.trim();
        if (!trimmed) {
          throw new Error("Write a JAML filter first.");
        }
        const validation = Motely.validateJaml(trimmed);
        if (validation !== "valid") {
          throw new Error(String(validation ?? "Invalid JAML"));
        }
        const t0 = performance.now();
        const result = Motely.analyzeJamlSeeds(trimmed, []);
        const elapsedMs = performance.now() - t0;
        if (cancelled) return;
        if (result.error) {
          throw new Error(result.error);
        }
        setLoad({ status: "ready", result, elapsedMs });
      } catch (error) {
        if (cancelled) return;
        setLoad({
          status: "error",
          message: error instanceof Error ? error.message : String(error),
        });
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [jaml]);

  const rows = useMemo(
    () => (load.status === "ready" ? load.result.seeds : []),
    [load],
  );
  const seedList = useMemo(() => rows.map((row) => row.seed), [rows]);
  const safeIndex = seedList.length > 0 ? Math.min(index, seedList.length - 1) : 0;
  const current = rows[safeIndex];
  const activeSeed = current?.seed ?? "";

  const handleSeedChange = (seed: string) => {
    const nextIndex = seedList.indexOf(seed);
    if (nextIndex >= 0) setIndex(nextIndex);
  };

  const rootClass = ["j-jamlyzer", className].filter(Boolean).join(" ");

  if (load.status === "loading") {
    return (
      <div className={rootClass} style={style}>
        <JimboPanel className="j-jamlyzer__panel j-jamlyzer__panel--hint">
          <JimboText size="sm" tone="white" className="j-text-center">
            Analyzing seeds…
          </JimboText>
        </JimboPanel>
      </div>
    );
  }

  if (load.status === "error") {
    return (
      <div className={rootClass} style={style}>
        <JimboPanel className="j-jamlyzer__panel j-jamlyzer__panel--hint">
          <JimboText size="xs" tone="red" className="j-text-center">
            {load.message}
          </JimboText>
        </JimboPanel>
      </div>
    );
  }

  const { elapsedMs, result } = load;
  const matchCount = rows.filter(seedMatches).length;
  const isMatch = current ? seedMatches(current) : false;
  const tallyLine =
    result.tallyLabels && result.tallyLabels.length > 0 && current
      ? result.tallyLabels.map((label, i) => `${label}: ${current.tallies[i] ?? 0}`).join(" · ")
      : null;

  return (
    <div className={rootClass} style={style}>
      <JimboText size="xs" tone="white" className="j-jamlyzer__stats j-text-center">
        {elapsedMs.toFixed(0)} ms · {rows.length} seeds · {matchCount} match
      </JimboText>

      <div className="j-jamlyzer__spinner">
        <JamlSeedSpinner
          seeds={seedList}
          value={activeSeed}
          onChange={handleSeedChange}
          label=""
          placeholder="Add seeds: to JAML"
          variant="dark"
          aria-label="Jamlyzer seed"
        />
      </div>

      {current ? (
        <>
          <JimboPanel
            className={[
              "j-jamlyzer__panel",
              "j-jamlyzer__panel--verdict",
              isMatch ? "j-jamlyzer__panel--match j-glow--match" : "j-jamlyzer__panel--miss",
            ].join(" ")}
          >
            <JimboText size="md" tone={isMatch ? "green" : "red"} className="j-text-center">
              {isMatch ? `Match · score ${current.score}` : `No match · score ${current.score}`}
            </JimboText>
          </JimboPanel>

          {tallyLine ? (
            <JimboInnerPanel className="j-jamlyzer__tallies">
              <JimboText size="xs" tone="white" className="j-text-center">
                {tallyLine}
              </JimboText>
            </JimboInnerPanel>
          ) : null}
        </>
      ) : (
        <JimboPanel className="j-jamlyzer__panel j-jamlyzer__panel--hint">
          <JimboText size="xs" tone="white" className="j-text-center">
            No seeds in JAML. Run Motely CLI with --save-seeds or add a seeds: list.
          </JimboText>
        </JimboPanel>
      )}
    </div>
  );
}
