"use client";

import React, { useEffect, useMemo, useState } from "react";
import {
  MotelyJaml,
  MotelyJamlyzer,
  type MotelyJamlyzerSeedResult,
} from "motely-wasm";
import { ensureMotelyReady } from "../lib/motely/runtime.js";
import { fromJaml } from "../lib/motely/jamlParse.js";
import { JimboPanel } from "../ui/JimboPanel.js";
import { JimboText } from "../ui/jimboText.js";
import { JimboStack } from "../ui/JimboLayout.js";
import { JamlSeedSpinner } from "./JamlSeedSpinner.js";
import { JamlyzerAnteDetails } from "./jamlyzer/JamlyzerAnteDetails.js";

export interface JamlyzerProps {
  /** Full JAML document. Seeds come from the top-level `seeds:` array via Motely. */
  jaml: string;
  className?: string;
  style?: React.CSSProperties;
}

type JamlyzerLoadState =
  | { status: "loading" }
  | { status: "ready"; seeds: readonly MotelyJamlyzerSeedResult[]; elapsedMs: number }
  | { status: "error"; message: string };

function seedMatches(row: MotelyJamlyzerSeedResult): boolean {
  return (row.score ?? 0) >= 1;
}

const MIN_ANTE = 1;
const MAX_ANTE = 8;

export function Jamlyzer({ jaml, className = "", style }: JamlyzerProps) {
  const [load, setLoad] = useState<JamlyzerLoadState>({ status: "loading" });
  const [index, setIndex] = useState(0);
  const [selectedAnte, setSelectedAnte] = useState(MIN_ANTE);
  const [lastJaml, setLastJaml] = useState(jaml);

  // Reset to loading the moment `jaml` changes — render-phase derivation
  // (React's "Adjusting state when a prop changes" pattern) avoids the
  // cascading render that synchronous setState-in-effect would cause.
  if (jaml !== lastJaml) {
    setLastJaml(jaml);
    setLoad({ status: "loading" });
    setIndex(0);
    setSelectedAnte(MIN_ANTE);
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
        const validation = MotelyJaml.validate(trimmed);
        if (validation !== "valid") {
          throw new Error(String(validation ?? "Invalid JAML"));
        }
        const t0 = performance.now();
        const seeds = MotelyJamlyzer.analyzeSeeds(fromJaml(trimmed));
        const elapsedMs = performance.now() - t0;
        if (cancelled) return;
        setLoad({ status: "ready", seeds, elapsedMs });
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
    () => (load.status === "ready" ? load.seeds : []),
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
      <JimboStack gap="sm" align="stretch" className={rootClass} style={style}>
        <JimboPanel className="j-jamlyzer__panel j-jamlyzer__panel--hint">
          <JimboText size="sm" tone="white" className="j-text-center">
            Analyzing seeds…
          </JimboText>
        </JimboPanel>
      </JimboStack>
    );
  }

  if (load.status === "error") {
    return (
      <JimboStack gap="sm" align="stretch" className={rootClass} style={style}>
        <JimboPanel className="j-jamlyzer__panel j-jamlyzer__panel--hint">
          <JimboText size="xs" tone="red" className="j-text-center">
            {load.message}
          </JimboText>
        </JimboPanel>
      </JimboStack>
    );
  }

  const { elapsedMs } = load;
  const matchCount = rows.filter(seedMatches).length;
  const isMatch = current ? seedMatches(current) : false;

  const hasAnalysis = !!current?.antes?.length;

  return (
    <JimboStack gap="sm" align="stretch" className={rootClass} style={style}>
      <JimboText size="xs" tone="white" className="j-jamlyzer__stats j-text-center">
        {elapsedMs.toFixed(0)} ms · {rows.length} seeds · {matchCount} match
      </JimboText>

      <JamlSeedSpinner
        seeds={seedList}
        value={activeSeed}
        onChange={handleSeedChange}
        label=""
        placeholder="Add seeds: to JAML"
        variant="dark"
        aria-label="Jamlyzer seed"
      />

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

          {hasAnalysis && current.antes && (
            <JamlyzerAnteDetails
              ante={current.antes.find((a) => a.ante === selectedAnte)}
              selectedAnte={selectedAnte}
              minAnte={MIN_ANTE}
              maxAnte={MAX_ANTE}
              onSelectAnte={setSelectedAnte}
            />
          )}
        </>
      ) : (
        <JimboPanel className="j-jamlyzer__panel j-jamlyzer__panel--hint">
          <JimboText size="xs" tone="white" className="j-text-center">
            No seeds in JAML. Run Motely CLI with --save-seeds or add a seeds: list.
          </JimboText>
        </JimboPanel>
      )}
    </JimboStack>
  );
}
