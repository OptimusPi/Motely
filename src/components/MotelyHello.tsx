"use client";

import { useCallback, useState } from "react";
import { Motely } from "motely-wasm";
import type { IMotelySearch, MotelyProgress, MotelyScoredSeedResult } from "motely-wasm/motely";
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
  const [searchRef, setSearchRef] = useState<IMotelySearch | null>(null);

  const handleStart = useCallback(async () => {
    setError(null);
    setSeeds([]);
    setSearched(0n);
    setMatched(0n);
    setStatus("running");

    const validation = Motely.validateJaml(jaml);
    if (validation !== "valid") {
      setError(validation);
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

    Motely.onScoredResult.subscribe(onScored);
    Motely.onProgress.subscribe(onProg);

    try {
      const search = Motely.fromJaml(jaml).withRandomSearch(searchCount).start(undefined);
      setSearchRef(search);
      await search.waitForCompletionAsync(undefined);
      setStatus(search.isCompleted ? "done" : "idle");
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setStatus("error");
    } finally {
      Motely.onScoredResult.unsubscribe(onScored);
      Motely.onProgress.unsubscribe(onProg);
      setSearchRef(null);
    }
  }, [jaml, searchCount]);

  const handleStop = useCallback(() => {
    searchRef?.cancel();
  }, [searchRef]);

  const statusTone =
    status === "running" ? "blue" : status === "done" ? "green" : status === "error" ? "red" : "dark";

  return (
    <JimboPanel>
      <JimboStack gap="md">
        <JimboRow gap="sm" align="center">
          <JimboText size="xs" tone="grey">motely v</JimboText>
          <JimboText size="md" tone="gold">{Motely.version()}</JimboText>
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
