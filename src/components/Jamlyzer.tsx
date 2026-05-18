"use client";
import React, { useState } from "react";
import { JimboPanel, JimboButton } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { JamlSeedSpinner } from "./JamlSeedSpinner.js";
import type { JamlSeedInputVariant } from "./JamlSeedInput.js";

export interface JamlyzerProps {
  jaml: string;
  onTest: (seed: string) => void;
  result: "idle" | "match" | "nomatch" | "running" | "error";
  error?: string | null;
  seeds?: string[];
  initialSeed?: string;
  seedVariant?: JamlSeedInputVariant;
}

export function Jamlyzer({ jaml, onTest, result, error, seeds = [], initialSeed = "", seedVariant = "dark" }: JamlyzerProps) {
  const [seed, setSeed] = useState(initialSeed || seeds[0] || "");

  const handleTest = () => {
    const s = seed.trim().toUpperCase();
    if (!s) return;
    onTest(s);
  };

  return (
    <div className="j-jamlyzer">
      <div className="j-jamlyzer__control">
          <JamlSeedSpinner
            seeds={seeds}
            value={seed}
            onChange={setSeed}
            label="Seed"
            placeholder="Aleeb"
            variant={seedVariant}
            onKeyDown={(e) => e.key === "Enter" && handleTest()}
            aria-label="Jamlyzer seed"
          />
          <JimboButton
            tone={result === "running" ? "red" : "orange"}
            size="sm"
            onClick={handleTest}
            disabled={!seed.trim() || !jaml.trim()}
          >
            {result === "running" ? "..." : "Test"}
          </JimboButton>
      </div>

      {result === "match" && (
        <JimboPanel className="j-jamlyzer__result j-jamlyzer__result--match j-glow--match">
          <JimboText size="xl" tone="gold" className="j-jamlyzer__seed">{seed}</JimboText>
          <JimboText size="md" tone="green">Match</JimboText>
        </JimboPanel>
      )}

      {result === "nomatch" && (
        <JimboPanel className="j-jamlyzer__result">
          <JimboText size="xl" tone="grey" className="j-jamlyzer__seed">{seed}</JimboText>
          <JimboText size="md" tone="red">No match</JimboText>
        </JimboPanel>
      )}

      {result === "error" && (
        <JimboPanel className="j-jamlyzer__result">
          <JimboText size="xs" tone="red" className="j-text-center">{error ?? "Error"}</JimboText>
        </JimboPanel>
      )}

      {result === "idle" && !jaml.trim() && (
        <JimboPanel className="j-jamlyzer__result">
          <JimboText size="xs" tone="grey" className="j-text-center">
            Write a JAML filter in the JAML tab first
          </JimboText>
        </JimboPanel>
      )}
    </div>
  );
}
