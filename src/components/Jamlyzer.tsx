"use client";
import React, { useState } from "react";
import { JimboPanel, JimboButton } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { JimboColorOption } from "../ui/tokens.js";
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
    <div style={{ padding: 10, display: "flex", flexDirection: "column", gap: 8 }}>
      {/* Seed input */}
      <JimboPanel>
        <div style={{ display: "flex", gap: 6, alignItems: "flex-end", justifyContent: "space-between" }}>
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
      </JimboPanel>

      {/* Result */}
      {result === "match" && (
        <JimboPanel className="j-glow--match" style={{ background: JimboColorOption.DARK_GREEN, textAlign: "center" }}>
          <JimboText size="xl" tone="gold" style={{ letterSpacing: 3, display: "block", marginBottom: 4 }}>{seed}</JimboText>
          <JimboText size="md" tone="green">Match</JimboText>
        </JimboPanel>
      )}

      {result === "nomatch" && (
        <JimboPanel style={{ textAlign: "center" }}>
          <JimboText size="xl" tone="grey" style={{ letterSpacing: 3, display: "block", marginBottom: 4 }}>{seed}</JimboText>
          <JimboText size="md" tone="red">No match</JimboText>
        </JimboPanel>
      )}

      {result === "error" && (
        <JimboPanel>
          <JimboText size="xs" tone="red" style={{ display: "block", textAlign: "center" }}>{error ?? "Error"}</JimboText>
        </JimboPanel>
      )}

      {result === "idle" && !jaml.trim() && (
        <JimboPanel>
          <JimboText size="xs" tone="grey" style={{ display: "block", textAlign: "center" }}>
            Write a JAML filter in the JAML tab first
          </JimboText>
        </JimboPanel>
      )}
    </div>
  );
}
