"use client";

import React, { useEffect, useMemo, useState } from "react";
import { JimboButton, JimboPanel } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { JimboColorOption } from "../ui/tokens.js";
import { JimboFlankNav } from "../ui/jimboFlankNav.js";
import { JamlMapEditor } from "./jamlMap/JamlMapEditor.js";
import { JamlAnalyzerFullscreen } from "./JamlAnalyzerFullscreen.js";
import { useSearch } from "../hooks/useSearch.js";
import { useAnalyzer } from "../hooks/useAnalyzer.js";
import { visualFilterToJamlText } from "../utils/jamlVisualFilter.js";
import { JamlSpeedometer } from "./JamlSpeedometer.js";

const C = JimboColorOption;

export interface JamlCuratorProps {
}

export function JamlCurator({ }: JamlCuratorProps) {
  // Use map editor by default to generate JAML
  const [jamlText, setJamlText] = useState("");
  const search = useSearch();
  const analyzer = useAnalyzer();

  // Search results pagination
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
    if (currentSeed) {
      analyzer.analyze(currentSeed, "Red", "White", jamlText);
    }
  }, [currentSeed, jamlText]); // eslint-disable-line react-hooks/exhaustive-deps

  // Map Editor changes
  const handleMapChange = (jamlString: string) => {
    setJamlText(jamlString);
  };

  return (
    <div style={{
      width: "100%",
      maxWidth: 375,
      height: "100svh",
      margin: "0 auto",
      position: "relative",
      background: C.DARKEST,
      overflow: "hidden",
      borderLeft: `1px solid ${C.PANEL_EDGE}`,
      borderRight: `1px solid ${C.PANEL_EDGE}`,
      boxShadow: `0 0 20px rgba(0,0,0,0.5)`,
    }}>
      <JamlAnalyzerFullscreen
        antes={analyzer.antes}
        live={analyzer.live}
        hidePicker
        topPage={
          <section style={{
            width: "100%",
            height: "100svh",
            scrollSnapAlign: "start",
            display: "flex",
            flexDirection: "column",
            gap: 12,
            padding: "16px 12px 24px",
            boxSizing: "border-box",
            borderBottom: `2px solid ${C.GOLD}`,
          }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
               <JimboText size="lg" tone="gold">JAML Curator</JimboText>
               <JimboButton tone={isSearching ? "red" : "green"} size="sm" onClick={handleSearch}>
                 {isSearching ? "STOP" : "SEARCH"}
               </JimboButton>
            </div>

            <div style={{ flex: 1, minHeight: 0, overflowY: 'auto' }} className="hide-scrollbar">
              <JamlMapEditor onChange={handleMapChange} />
            </div>

            <div style={{ flexShrink: 0 }}>
              <JamlSpeedometer 
                status={search.status}
                seedsPerSecond={search.seedsPerSecond}
                totalSearched={search.totalSearched}
                matchingSeeds={search.matchingSeeds}
              />
            </div>

            <div style={{ flexShrink: 0 }}>
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

                    <JimboText size="micro" tone="grey" className="j-text-center" style={{ opacity: 0.7, marginTop: 8 }}>
                      ▼ SWIPE DOWN FOR ANTES ▼
                    </JimboText>
                  </div>
                )}
              </JimboPanel>
            </div>
          </section>
        }
      />
    </div>
  );
}
