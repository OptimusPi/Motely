/// <reference types="vite/client" />
import React, { useEffect, useState, useCallback } from "react";
import {
  JamlAnalyzerFullscreen,
  JamlAestheticSelector,
  JamlMapEditor,
  JimboBackground,
  JimboButton,
  JimboPanel,
  JimboText,
  JimboFlankNav,
  JimboTabs,
  Showcase,
  useAnalyzer,
  useSearch,
  type JamlAestheticOption,
  type ShowcaseFilter,
} from "../src/index.ts";
import { JimboApp, JimboAppFooter } from "../src/ui/jimboApp.tsx";
import { JamlSpeedometer } from "../src/components/JamlSpeedometer.tsx";
import "./App.css";

// Boot motely once globally
import motely from "motely-wasm";
motely.boot().catch(console.error);

type Route = "home" | "search" | "editor";
type SearchMode = "sequential" | "random" | "aesthetic";

// ─────────────────────────────────────────────────────────────────────────────
// Sample data — Hot Filters, Recent Finds
// ─────────────────────────────────────────────────────────────────────────────

const HOT_FILTERS: ShowcaseFilter[] = [
  { name: "Perkeo Observatory", author: "Athuny & pifreak", hits: "12.4M", tone: "blue",  sample: ["perkeo","blueprint","brainstorm"] },
  { name: "The Daily Wee",      author: "pifreak",          hits: "3.1M",  tone: "red",   sample: ["wee_joker","hanging_chad","hack"] },
  { name: "Lucky Cat",          author: "JamlGenie",        hits: "128K",  tone: "gold",  sample: ["lucky_cat","egg"] },
  { name: "Fool Emperor Chain", author: "ope",              hits: "42K",   tone: "green", sample: ["showman"] },
];

const RECENT_FINDS = [
  { seed: "X1B8TW4J", filterName: "Perkeo Observatory", score: 8 },
  { seed: "ALEPH999", filterName: "Lucky Cat",          score: 5 },
  { seed: "UFX3G111", filterName: "Lucky Cat",          score: 4 },
  { seed: "BETAZ3RO", filterName: "Daily Wee",          score: 3 },
];

// ─────────────────────────────────────────────────────────────────────────────
// Home — Showcase landing
// ─────────────────────────────────────────────────────────────────────────────

function HomePage({ onNavigate }: { onNavigate: (r: Route) => void }) {
  return (
    <Showcase
      title="Balatro"
      subtitle="Seed Curator"
      hotFilters={HOT_FILTERS}
      recentFinds={RECENT_FINDS}
      stats={{ searched: "15.6B", matches: "2,847", speed: "5.4M/s" }}
      mcpInfo={{
        runtime: "Standalone",
        engine: "motely-wasm",
        features: "SIMD · Workers · JAML",
      }}
      onNewSearch={() => onNavigate("search")}
      onBrowseFilters={() => {/* TODO: filter browser view */}}
      onFilterClick={() => {
        // TODO: load filter and navigate to search
        onNavigate("search");
      }}
    />
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Search — compact, thumb-zone nav at bottom
// ─────────────────────────────────────────────────────────────────────────────

function SearchPage({ onBack }: { onBack: () => void }) {
  const [jamlText, setJamlText] = useState("");
  const [searchMode, setSearchMode] = useState<SearchMode>("random");

  const [aesthetic, setAesthetic] = useState<JamlAestheticOption | null>(null);
  const [resultIndex, setResultIndex] = useState(0);
  const [showEditor, setShowEditor] = useState(false);

  const search = useSearch();
  const analyzer = useAnalyzer();
  const isSearching = search.status === "running";

  const handleSearch = useCallback(() => {
    if (isSearching) { search.cancel(); return; }
    setResultIndex(0);
    if (searchMode === "random") {
      search.start(jamlText, 1_000_000);
    } else if (searchMode === "aesthetic" && aesthetic) {
      const idx = ["Palindrome", "Psychosis", "Gross", "Nsfw", "Funny", "Balatro"].indexOf(aesthetic);
      search.startAesthetic(jamlText, idx >= 0 ? idx : 0);
    } else {
      search.start(jamlText, 1_000_000);
    }
  }, [isSearching, searchMode, jamlText, aesthetic, search]);

  const currentSeed = search.results[resultIndex]?.seed;
  const currentScore = search.results[resultIndex]?.score;
  useEffect(() => {
    if (currentSeed) analyzer.analyze(currentSeed, "Red", "White", jamlText);
  }, [currentSeed, jamlText]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleCopySeed = useCallback(() => {
    if (currentSeed) navigator.clipboard?.writeText(currentSeed);
  }, [currentSeed]);

  // Editor sub-view
  if (showEditor) {
    return (
      <JimboApp>
        <div className="j-app__content" style={{ overflow: 'auto', scrollbarWidth: 'none' }}>
          <JimboText size="lg" tone="gold" style={{ marginBottom: 6 }}>JAML Filter</JimboText>
          <JamlMapEditor onChange={(j) => setJamlText(j)} />
        </div>
        <JimboAppFooter>
          <JimboButton tone="orange" fullWidth size="lg" onClick={() => setShowEditor(false)}>
            Done Editing
          </JimboButton>
        </JimboAppFooter>
      </JimboApp>
    );
  }

  // Analyzer view
  if (currentSeed && analyzer.antes.length > 0) {
    return (
      <JimboApp>
        <JamlAnalyzerFullscreen
          antes={analyzer.antes}
          live={analyzer.live}
          jaml={jamlText}
          hidePicker
          topPage={
            <section style={{
              width: "100%", height: "667px",
              scrollSnapAlign: "start",
              display: "flex", flexDirection: "column",
              gap: 4, padding: "6px 8px 8px", boxSizing: "border-box",
            }}>
              <JamlSpeedometer
                status={search.status} seedsPerSecond={search.seedsPerSecond}
                totalSearched={search.totalSearched} matchingSeeds={search.matchingSeeds}
              />
              <div style={{ flex: 1, display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
                <JimboPanel>
                  <JimboFlankNav
                    canPrev={resultIndex > 0}
                    canNext={resultIndex < search.results.length - 1}
                    onPrev={() => setResultIndex(i => Math.max(0, i - 1))}
                    onNext={() => setResultIndex(i => Math.min(search.results.length - 1, i + 1))}
                  >
                    <div className="j-flex-col j-items-center j-gap-xs">
                      <JimboText size="lg" tone="gold" style={{ letterSpacing: 2 }}>{currentSeed}</JimboText>
                      {currentScore != null && <JimboText size="xs" tone="green">score: {currentScore}</JimboText>}
                      <JimboButton tone="blue" size="xs" onClick={handleCopySeed}>Copy Seed</JimboButton>
                    </div>
                  </JimboFlankNav>
                  <JimboText size="micro" tone="grey" className="j-text-center" style={{ opacity: 0.7, marginTop: 4 }}>
                    ▼ swipe down for antes ▼
                  </JimboText>
                </JimboPanel>
              </div>
              <div className="j-flex j-gap-sm">
                <JimboButton tone={isSearching ? "red" : "green"} size="lg" onClick={handleSearch} style={{ flex: 1 }}>
                  {isSearching ? "Stop" : "Search"}
                </JimboButton>
                <JimboButton tone="orange" size="lg" onClick={onBack} style={{ flex: 1 }}>Back</JimboButton>
              </div>
            </section>
          }
        />
      </JimboApp>
    );
  }

  // Search setup view
  return (
    <JimboApp>
      <div className="j-app__content">
        <JimboText size="lg" tone="gold" style={{ marginBottom: 4 }}>Seed Search</JimboText>

        {/* Mode */}
        <JimboTabs
          tabs={[
            { id: 'random', label: 'Random' },
            { id: 'aesthetic', label: 'Aesthetic' },
          ]}
          activeTab={searchMode}
          onTabChange={(id) => setSearchMode(id as SearchMode)}
          style={{ marginBottom: 6 }}
        />

        {searchMode === "aesthetic" && (
          <div style={{ marginBottom: 6 }}>
            <JamlAestheticSelector value={aesthetic} onChange={(a) => setAesthetic(a)} />
          </div>
        )}

        {/* JAML filter */}
        <JimboButton tone="blue" size="sm" fullWidth onClick={() => setShowEditor(true)} style={{ marginBottom: 6 }}>
          {jamlText ? "Edit JAML Filter ✓" : "Set JAML Filter..."}
        </JimboButton>

        {/* Stats */}
        <JamlSpeedometer
          status={search.status} seedsPerSecond={search.seedsPerSecond}
          totalSearched={search.totalSearched} matchingSeeds={search.matchingSeeds}
          style={{ marginBottom: 6 }}
        />

        {search.error && (
          <JimboPanel style={{ marginBottom: 6 }}>
            <JimboText size="sm" tone="red">{search.error}</JimboText>
          </JimboPanel>
        )}

        {/* Results */}
        {search.results.length > 0 && (
          <JimboPanel style={{ flex: 1, minHeight: 0, overflow: 'hidden' }}>
            <div className="j-flex j-items-center j-justify-between" style={{ marginBottom: 2 }}>
              <JimboText size="xs" tone="grey">Matches</JimboText>
              <JimboText size="xs" tone="gold">{String(search.matchingSeeds)}</JimboText>
            </div>
            {search.results.slice(0, 6).map((r, i) => (
              <div key={r.seed} className="j-flex j-items-center j-justify-between"
                style={{ cursor: 'pointer', padding: '1px 0' }} onClick={() => setResultIndex(i)}>
                <JimboText size="body" tone={i === resultIndex ? "gold" : "white"}>{r.seed}</JimboText>
                <JimboText size="xs" tone="green">+{r.score}</JimboText>
              </div>
            ))}
          </JimboPanel>
        )}
      </div>

      <JimboAppFooter>
        <div className="j-flex-col j-gap-sm">
          <JimboButton tone={isSearching ? "red" : "green"} fullWidth size="lg" onClick={handleSearch}
            disabled={searchMode === "aesthetic" && !aesthetic}>
            {isSearching ? "Stop Cooking" : "Let Jimbo Cook!"}
          </JimboButton>
          <JimboButton tone="orange" fullWidth size="lg" onClick={onBack}>Back</JimboButton>
        </div>
      </JimboAppFooter>
    </JimboApp>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main
// ─────────────────────────────────────────────────────────────────────────────

export function App() {
  const [route, setRoute] = useState<Route>("home");
  return (
    <div className="jaml-demo-root">
      <JimboBackground />
      <div className="jaml-demo-content-wrapper">
        {route === "home" && <HomePage onNavigate={setRoute} />}
        {route === "search" && <SearchPage onBack={() => setRoute("home")} />}
      </div>
    </div>
  );
}
