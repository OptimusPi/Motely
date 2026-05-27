"use client";

import { useCallback, useState } from "react";
import { Motely } from "motely-wasm";
import type { IMotelySearch, MotelyProgress, MotelyScoredSeedResult } from "motely-wasm/motely";
import { MotelyDeck, MotelyStake } from "motely-wasm/motely/enums";
import { JimboPanel, JimboButton } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { JimboStack, JimboRow } from "../ui/jimboLayout.js";
import { JimboBadge } from "../ui/JimboBadge.js";
import { RunConfigModal } from "./RunConfigModal.js";

const DEFAULT_JAML = `must:
  - joker: Blueprint
    antes: [1, 2, 3, 4, 5, 6, 7, 8]
`;

export interface SeedFinderAppProps {
  initialJaml?: string;
  initialDeck?: keyof typeof MotelyDeck;
  initialStake?: keyof typeof MotelyStake;
}

export function SeedFinderApp({
  initialJaml = DEFAULT_JAML,
  initialDeck = "Red",
  initialStake = "White",
}: SeedFinderAppProps) {
  const [jaml] = useState(initialJaml);
  const [deck, setDeck] = useState<keyof typeof MotelyDeck>(initialDeck);
  const [stake, setStake] = useState<keyof typeof MotelyStake>(initialStake);
  const [seeds, setSeeds] = useState<string[]>([]);
  const [searched, setSearched] = useState<bigint>(0n);
  const [matched, setMatched] = useState<bigint>(0n);
  const [status, setStatus] = useState<"idle" | "running" | "done" | "error">("idle");
  const [error, setError] = useState<string | null>(null);
  const [searchRef, setSearchRef] = useState<IMotelySearch | null>(null);
  const [modalOpen, setModalOpen] = useState(false);

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
      const search = Motely.fromJaml(jaml)
        .withDeck(MotelyDeck[deck as keyof typeof MotelyDeck])
        .withStake(MotelyStake[stake as keyof typeof MotelyStake])
        .withSequentialSearch()
        .start(undefined);
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
  }, [jaml, deck, stake]);

  const handleStop = useCallback(() => {
    searchRef?.cancel();
  }, [searchRef]);

  const isRunning = status === "running";
  const statusTone: "dark" | "blue" | "green" | "red" =
    status === "running" ? "blue" : status === "done" ? "green" : status === "error" ? "red" : "dark";

  return (
    <JimboPanel>
      <JimboStack gap="md">
        <JimboRow gap="sm" align="center">
          <JimboText size="xs" tone="grey">motely v</JimboText>
          <JimboText size="md" tone="gold">{Motely.version()}</JimboText>
          <JimboBadge size="sm" tone={statusTone}>{status}</JimboBadge>
        </JimboRow>

        <JimboButton
          tone="grey"
          size="sm"
          fullWidth
          disabled={isRunning}
          onClick={() => setModalOpen(true)}
        >
          {deck} Deck · {stake} Stake
        </JimboButton>

        {isRunning ? (
          <JimboButton tone="red" size="lg" fullWidth onClick={handleStop}>
            Stop Search
          </JimboButton>
        ) : (
          <JimboButton tone="blue" size="lg" fullWidth onClick={handleStart}>
            Let Jimbo COOK!
          </JimboButton>
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

      <RunConfigModal
        open={modalOpen}
        onClose={() => setModalOpen(false)}
        deck={deck}
        stake={stake}
        onChange={(d, s) => {
          setDeck(d as keyof typeof MotelyDeck);
          setStake(s as keyof typeof MotelyStake);
        }}
      />
    </JimboPanel>
  );
}
