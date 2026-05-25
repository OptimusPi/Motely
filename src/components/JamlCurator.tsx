"use client";

// TODO(jimbo-primitives): pre-dates no-inline-style / no-token-in-jsx-style /
// no-inline-component rules. Refactor to compose from Jimbo* primitives once
// screenshot-driven primitive design lands. `git grep TODO(jimbo-primitives)`.
/* eslint-disable jaml-design/no-inline-style */

import React, { useState } from "react";
import { JimboButton, JimboPanel } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { JimboFlankNav } from "../ui/jimboFlankNav.js";
import { JimboApp, JimboAppFooter, JimboAppScroll } from "../ui/jimboApp.js";
import { JamlMapEditor } from "./jamlMap/JamlMapEditor.js";
import { useSearch } from "../hooks/useSearch.js";
import { JamlSpeedometer } from "./JamlSpeedometer.js";

export function JamlCurator() {
  const [jamlText, setJamlText] = useState("");
  const search = useSearch();
  const [resultIndex, setResultIndex] = useState(0);

  const isSearching = search.status === "running";

  const handleSearch = () => {
    if (isSearching) {
      search.cancel();
    } else {
      setResultIndex(0);
      search.startRandom(jamlText, 1_000_000);
    }
  };

  const currentSeed = search.results[resultIndex]?.seed;

  const handleCopySeed = () => {
    if (currentSeed && typeof navigator !== "undefined" && navigator.clipboard) {
      void navigator.clipboard.writeText(currentSeed);
    }
  };

  return (
    <JimboApp>
      <div
        className="j-flex j-items-center j-justify-between"
        style={{
          flexShrink: 0,
          padding: "10px 12px",
          borderBottom: "2px solid var(--j-gold)",
        }}
      >
        <JimboText size="lg" tone="gold">JAML Curator</JimboText>
      </div>

      <JimboAppScroll>
        <div className="j-flex-col j-gap-md">
          <JamlMapEditor onChange={(s) => setJamlText(s)} />

          <JamlSpeedometer
            status={search.status}
            seedsPerSecond={search.seedsPerSecond}
            totalSearched={search.totalSearched}
            matchingSeeds={search.matchingSeeds}
          />

          <JimboPanel>
            {search.results.length === 0 ? (
              <JimboText size="sm" tone="grey" className="j-text-center">
                {isSearching ? "Searching..." : "No results yet."}
              </JimboText>
            ) : (
              <div className="j-flex-col j-gap-sm">
                <div className="j-flex j-items-center j-justify-between">
                  <JimboText size="xs" tone="grey">Seed matches</JimboText>
                  <JimboText size="xs" tone="gold">{search.matchingSeeds} found</JimboText>
                </div>
                <JimboFlankNav
                  canPrev={resultIndex > 0}
                  canNext={resultIndex < search.results.length - 1}
                  onPrev={() => setResultIndex(i => Math.max(0, i - 1))}
                  onNext={() => setResultIndex(i => Math.min(search.results.length - 1, i + 1))}
                >
                  <div className="j-flex-col j-items-center j-gap-xs">
                    <JimboText size="lg" tone="gold" style={{ letterSpacing: 2 }}>{currentSeed}</JimboText>
                    <JimboButton tone="blue" size="xs" onClick={handleCopySeed}>Copy Seed</JimboButton>
                  </div>
                </JimboFlankNav>
              </div>
            )}
          </JimboPanel>
        </div>
      </JimboAppScroll>

      <JimboAppFooter>
        <JimboButton
          tone={isSearching ? "red" : "green"}
          size="md"
          fullWidth
          onClick={handleSearch}
        >
          {isSearching ? "Stop Searching" : "Search Seeds"}
        </JimboButton>
      </JimboAppFooter>
    </JimboApp>
  );
}
