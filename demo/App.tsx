import React, { useState, useMemo } from "react";
import { JamlIde, useSearch, type JamlIdeSearchResult } from "jaml-ui";
import { JimboColorOption } from "jaml-ui/ui";
import motelyWasmUrl from "motely-wasm/index.mjs?url";

const MOTELY_WASM_VERSION: string = "local";

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

const MOTELY_WASM_URL = motelyWasmUrl;

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
    <div style={{ padding: 12, display: "flex", flexDirection: "column", height: "100%", width: "100%", maxWidth: 1100, margin: "0 auto" }}>
      <JamlIde
        jaml={jaml}
        onChange={setJaml}
        searchResults={results}
        onSearch={handleSearch}
        isSearching={isSearching}
        style={{ flex: 1 }}
      />
    </div>
  );
}
