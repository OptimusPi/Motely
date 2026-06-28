"use client";

import { useCallback, useRef, useState } from "react";
import { MotelySearch } from "motely-wasm";
import type { MotelyProgress, MotelyScoredSeedResult } from "motely-wasm";
import { ensureMotelyReady, parseJaml, runSearch } from "../lib/motely/runtime.js";
import { JimboPanel, JimboButton } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { JimboStack, JimboRow } from "../ui/jimboLayout.js";
import { JimboBadge } from "../ui/JimboBadge.js";

const STARTER_JAML = `must:
  - joker: Blueprint
    antes: [1, 2, 3, 4, 5, 6, 7, 8]
deck: Red
stake: White
`;

export interface MotelyHelloProps {
  jaml?: string;
  searchCount?: number;
}

export function MotelyHello({ jaml = STARTER_JAML, searchCount = 5000 }: MotelyHelloProps) {
  const [seeds, setSeeds] = useState<string[]>([]);
  const [searched, setSearched] = useState<bigint>(0n);
  const [matched, setMatched] = useState<bigint>(0n);
  const [status, setStatus] = useState<"idle" | "running" | "done" | "error">("idle");
  const [error, setError] = useState<string | null>(null);
  const cancelledRef = useRef(false);

  const handleStart = useCallback(async () => {
    setError(null);
    setSeeds([]);
    setSearched(0n);
    setMatched(0n);
    setStatus("running");
    cancelledRef.current = false;

    await ensureMotelyReady();

    let config;
    try {
      config = parseJaml(jaml);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Invalid JAML");
      setStatus("error");
      return;
    }

    const onScored = (r: MotelyScoredSeedResult) => {
      setSeeds((prev) => [r.seed, ...prev].slice(0, 8));
    };
    const onProg = (p: MotelyProgress) => {
      setSearched(p.seedsSearched);
      setMatched(p.matchingSeeds);
    };

    MotelySearch.onScoredResult.subscribe(onScored);
    MotelySearch.onProgress.subscribe(onProg);

    try {
      await runSearch(config, "random", { count: searchCount });
      setStatus(cancelledRef.current ? "idle" : "done");
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setStatus("error");
    } finally {
      MotelySearch.onScoredResult.unsubscribe(onScored);
      MotelySearch.onProgress.unsubscribe(onProg);
    }
  }, [jaml, searchCount]);

  const handleStop = useCallback(() => {
    // motely-wasm@23 has no engine-level cancel; mark cancelled so the run resolves
    // to "idle". The in-flight random search finishes in the background.
    cancelledRef.current = true;
    setStatus("idle");
  }, []);

  const statusTone =
    status === "running" ? "blue" : status === "done" ? "green" : status === "error" ? "red" : "dark";

  return (
    <JimboPanel>
      <JimboStack gap="md">
        <JimboRow gap="sm" align="center">
          <JimboText size="xs" tone="grey">motely-wasm</JimboText>
          <JimboBadge size="sm" tone={statusTone}>{status}</JimboBadge>
        </JimboRow>

        <JimboText size="xs" tone="white">
          Blueprint · antes 1–8 · Red · White · {searchCount.toLocaleString()} seeds
        </JimboText>

        {status === "running" ? (
          <JimboButton tone="red" onClick={handleStop}>Stop</JimboButton>
        ) : (
          <JimboButton tone="orange" onClick={handleStart}>Find seeds</JimboButton>
        )}

        {searched > 0n ? (
          <JimboText size="xs" tone="grey">
            {searched.toString()} searched · {matched.toString()} hits
          </JimboText>
        ) : null}

        {error ? <JimboText size="sm" tone="red">{error}</JimboText> : null}

        {seeds.length > 0 ? (
          <JimboStack gap="xs">
            {seeds.map((s) => (
              <JimboText key={s} size="md" tone="gold">{s}</JimboText>
            ))}
          </JimboStack>
        ) : null}
      </JimboStack>
    </JimboPanel>
  );
}
