"use client";

import { useCallback, useState } from "react";
<<<<<<< HEAD
import { Motely, type IMotelySearch, type MotelyProgress, type MotelyScoredSeedResult, MotelyDeck, MotelyStake } from "motely-wasm";
import { JimboPanel, JimboButton } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { JimboStack, JimboRow } from "../ui/jimboLayout.js";
import { JimboBadge } from "../ui/JimboBadge.js";
import { RunConfigModal } from "./RunConfigModal.js";
=======
import { MotelyDeck, MotelyStake } from "motely-wasm/motely/enums";
import { JimboApp, JimboAppScroll } from "../ui/jimboApp.js";
import { JimboButton } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { JimboStack, JimboRow } from "../ui/jimboLayout.js";
import { JimboBadge } from "../ui/JimboBadge.js";
import { JamlIde } from "./JamlIde.js";
import { RunConfigModal } from "./RunConfigModal.js";
import { useSearch } from "../hooks/useSearch.js";
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45

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
<<<<<<< HEAD
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
        .start();
      setSearchRef(search);
      await search.waitForCompletionAsync();
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
=======
  const [jaml, setJaml] = useState(initialJaml);
  const [deck, setDeck] = useState<keyof typeof MotelyDeck>(initialDeck);
  const [stake, setStake] = useState<keyof typeof MotelyStake>(initialStake);
  const [modalOpen, setModalOpen] = useState(false);

  const { results, totalSearched, matchingSeeds, status, error, seedsPerSecond, startAesthetic, cancel } = useSearch();

  const isRunning = status === "running";

  const handleStart = useCallback(() => {
    const jamlWithConfig = `deck: ${deck}\nstake: ${stake}\n${jaml}`;
    startAesthetic(jamlWithConfig, 0);
  }, [jaml, deck, stake, startAesthetic]);

  const statusTone: "dark" | "blue" | "green" | "red" =
    status === "running" ? "blue" : status === "completed" ? "green" : status === "error" ? "red" : "dark";

  const searchResults = results.map((r) => ({ seed: r.seed, score: r.score, tallyColumns: r.tallyColumns }));

  return (
    <JimboApp>
      <JimboAppScroll>
        <JamlIde
          jaml={jaml}
          onChange={setJaml}
          defaultMode="visual"
          searchResults={searchResults}
          isSearching={isRunning}
          onSearch={handleStart}
          title="Seed Finder"
          actions={
            <JimboButton
              tone="grey"
              size="sm"
              disabled={isRunning}
              onClick={() => setModalOpen(true)}
            >
              {deck} · {stake}
            </JimboButton>
          }
          subtitle={
            <JimboRow gap="sm" align="center">
              <JimboBadge size="sm" tone={statusTone}>{status}</JimboBadge>
              {isRunning && seedsPerSecond > 0 ? (
                <JimboText size="xs" tone="grey">{Math.round(seedsPerSecond).toLocaleString()}/s</JimboText>
              ) : null}
              {totalSearched > 0n ? (
                <JimboText size="xs" tone="grey">{totalSearched.toLocaleString()} · {matchingSeeds.toString()} hits</JimboText>
              ) : null}
            </JimboRow>
          }
        />

        {error ? (
          <JimboStack gap="xs">
            <JimboText size="sm" tone="red">{error}</JimboText>
          </JimboStack>
        ) : null}

        {isRunning ? (
          <JimboButton tone="red" size="lg" fullWidth onClick={cancel}>Stop</JimboButton>
        ) : (
          <JimboButton tone="orange" size="lg" fullWidth onClick={handleStart}>
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
            Let Jimbo COOK!
          </JimboButton>
        )}

<<<<<<< HEAD
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
=======
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
      </JimboAppScroll>
    </JimboApp>
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
  );
}
