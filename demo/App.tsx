import React, { useState, useMemo } from "react";
import { JamlIde, useSearch, type JamlIdeSearchResult } from "jaml-ui";
import { JimboColorOption } from "jaml-ui/ui";
import motelyPkg from "motely-wasm/package.json" with { type: "json" };

const MOTELY_WASM_VERSION: string = motelyPkg.version;

const SAMPLE_JAML = `name: Blueprint Copy Engine
author: pifreak
description: Classic rare-joker copy stack anchoring on Blueprint + Brainstorm.
deck: Red
stake: White
must:
  - rareJoker: Blueprint
    antes: [1, 2, 3, 4]
should:
  - rareJoker: Brainstorm
    score: 80
  - legendaryJoker: Perkeo
    score: 70
  - uncommonJoker: Baron
    score: 55
  - tag: NegativeTag
    antes: [1, 2]
    score: 60
  - mixedJoker: Any
    edition: Negative
    score: 40
mustNot:
  - boss: ThePlant
    antes: [8]
`;

function envOrThrow(key: string): string {
  const v = (import.meta.env as Record<string, string | undefined>)[key];
  if (!v) throw new Error(`Missing ${key} in jaml-ui/.env.local — set VITE_CDN_BASE_URL (e.g. https://cdn.seedfinder.app)`);
  return v;
}

// Version comes from motely-wasm's own package.json — single source of truth.
// No env var lets version drift away from what npm installed.
const MOTELY_WASM_URL = `${envOrThrow("VITE_CDN_BASE_URL")}/motely-wasm/${MOTELY_WASM_VERSION}/index.mjs`;

export function App() {
  const [jaml, setJaml] = useState(SAMPLE_JAML);
  const search = useSearch(MOTELY_WASM_URL);

  // Feed real search results into JamlIde's expected shape.
  const results: JamlIdeSearchResult[] = useMemo(
    () => search.results.map((r) => ({ seed: r.seed, score: r.score })),
    [search.results],
  );

  const isSearching = search.status === "running";
  const bootStatus = search.status === "booting" ? "booting motely-wasm…" : null;

  const handleSearch = () => {
    if (isSearching) {
      search.cancel();
    } else {
      search.start(jaml, 5000);
    }
  };

  return (
    <div style={{ padding: 24, display: "grid", gap: 24, maxWidth: 1100, margin: "0 auto" }}>
      <header style={{ display: "flex", alignItems: "baseline", gap: 16 }}>
        <h1 style={{ fontSize: 20, margin: 0, color: JimboColorOption.GOLD_TEXT }}>jaml-ui demo</h1>
        <span style={{ fontSize: 11, color: JimboColorOption.GREY }}>
          motely-wasm @ {envOrThrow("VITE_MOTELY_WASM_VERSION")}
        </span>
        {bootStatus && <span style={{ fontSize: 11, color: JimboColorOption.GOLD_TEXT }}>{bootStatus}</span>}
        {search.status === "error" && (
          <span style={{ fontSize: 11, color: JimboColorOption.RED }}>error: {search.error}</span>
        )}
      </header>

      <JamlIde
        jaml={jaml}
        onChange={setJaml}
        searchResults={results}
        onSearch={handleSearch}
        isSearching={isSearching}
      />

      <footer style={{ fontSize: 11, color: JimboColorOption.DARK_GREY }}>
        status: {search.status} · searched: {search.totalSearched.toString()} · matches: {search.matchingSeeds.toString()} · results shown: {search.results.length}
      </footer>
    </div>
  );
}
