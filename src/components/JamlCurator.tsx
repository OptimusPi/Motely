"use client";

import React, { useEffect, useState } from "react";
import { JimboButton, JimboPanel } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { JimboColorOption } from "../ui/tokens.js";
import { JimboFlankNav } from "../ui/jimboFlankNav.js";
import { JamlMapEditor } from "./jamlMap/JamlMapEditor.js";
import { AnalyzerExplorer } from "./AnalyzerExplorer.js";
import { useSearch } from "../hooks/useSearch.js";
import { useAnalyzer } from "../hooks/useAnalyzer.js";
import { JamlSpeedometer } from "./JamlSpeedometer.js";

const C = JimboColorOption;

export function JamlCurator() {
  const [jamlText, setJamlText] = useState("");
  const search = useSearch();
  const analyzer = useAnalyzer();

  const [resultIndex, setResultIndex] = useState(0);

  const isSearching = search.status === "running";

  const handleSearch = () => {
    if (isSearching) {
      search.cancel();
    } else {
      setResultIndex(0);
      search.start(jamlText, 1_000_000);
    }
  };

  const currentSeed = search.results[resultIndex]?.seed;

  useEffect(() => {
    if (currentSeed && jamlText) {
      analyzer.analyze(currentSeed, jamlText);
    }
  }, [currentSeed, jamlText]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleMapChange = (jamlString: string) => {
    setJamlText(jamlString);
  };

  return (
    <div style={{
      width: "100%",
      maxWidth: 375,
      height: "100dvh",
      maxHeight: 667,
      margin: "0 auto",
      position: "relative",
      background: C.DARKEST,
      overflow: "hidden",
      borderLeft: `1px solid ${C.PANEL_EDGE}`,
      borderRight: `1px solid ${C.PANEL_EDGE}`,
      boxShadow: `0 0 20px rgba(0,0,0,0.5)`,
      display: "flex",
      flexDirection: "column",
    }}>
      <section style={{
        flexShrink: 0,
        display: "flex",
        flexDirection: "column",
        gap: 12,
        padding: "16px 12px 12px",
        boxSizing: "border-box",
        borderBottom: `2px solid ${C.GOLD}`,
      }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
          <JimboText size="lg" tone="gold">JAML Curator</JimboText>
          <JimboButton tone={isSearching ? "red" : "green"} size="sm" onClick={handleSearch}>
            {isSearching ? "STOP" : "SEARCH"}
          </JimboButton>
        </div>

        <div style={{ minHeight: 0, overflowY: "auto" }} className="hide-scrollbar">
          <JamlMapEditor onChange={handleMapChange} />
        </div>

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
                <JimboText size="xs" tone="grey">SEED MATCHES</JimboText>
                <JimboText size="xs" tone="gold">{search.matchingSeeds} FOUND</JimboText>
              </div>

              <JimboFlankNav
                canPrev={resultIndex > 0}
                canNext={resultIndex < search.results.length - 1}
                onPrev={() => setResultIndex(i => Math.max(0, i - 1))}
                onNext={() => setResultIndex(i => Math.min(search.results.length - 1, i + 1))}
              >
                <div className="j-flex-col j-items-center j-gap-xs">
                  <JimboText size="lg" tone="gold" style={{ letterSpacing: 2 }}>{currentSeed}</JimboText>
                  <JimboButton tone="blue" size="xs">Copy Seed</JimboButton>
                </div>
              </JimboFlankNav>
            </div>
          )}
        </JimboPanel>
      </section>

      <div style={{ flex: 1, minHeight: 0, overflowY: "auto" }} className="hide-scrollbar">
        <AnalyzerExplorer antes={analyzer.antes} jaml={jamlText} />
      </div>
    </div>
  );
}
