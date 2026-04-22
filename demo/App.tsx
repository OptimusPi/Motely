import React, { useState } from "react";
import { JamlIde, type JamlIdeSearchResult } from "jaml-ui";

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

const MOCK_RESULTS: JamlIdeSearchResult[] = [
  { seed: "8Q47WV6K", score: 320, tallyColumns: [1, 1, 1, 1, 1], tallyLabels: ["Blueprint", "Brainstorm", "Perkeo", "Baron", "NegativeTag"] },
  { seed: "DCVE28EC", score: 280, tallyColumns: [1, 0, 1, 1, 1], tallyLabels: ["Blueprint", "Brainstorm", "Perkeo", "Baron", "NegativeTag"] },
  { seed: "7M1GURT1", score: 215, tallyColumns: [1, 1, 0, 0, 1], tallyLabels: ["Blueprint", "Brainstorm", "Perkeo", "Baron", "NegativeTag"] },
];

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section style={{ display: "flex", flexDirection: "column", gap: 8 }}>
      <h2 style={{ fontSize: 12, letterSpacing: 2, color: "#8aa", textTransform: "uppercase", margin: 0 }}>{title}</h2>
      {children}
    </section>
  );
}

export function App() {
  const [controlledJaml, setControlledJaml] = useState(SAMPLE_JAML);
  const [isSearching, setIsSearching] = useState(false);

  const handleSearch = () => {
    setIsSearching(true);
    setTimeout(() => setIsSearching(false), 1200);
  };

  return (
    <div style={{ padding: 32, display: "grid", gap: 32, maxWidth: 1100, margin: "0 auto" }}>
      <header>
        <h1 style={{ fontSize: 20, margin: 0, color: "#eac058" }}>jaml-ui demo</h1>
        <div style={{ fontSize: 12, color: "#888" }}>Live component showcase — edits here hit the same source as the published package.</div>
      </header>

      <Section title="JamlIde · uncontrolled (defaultJaml only, no onChange)">
        <JamlIde defaultJaml={SAMPLE_JAML} />
      </Section>

      <Section title="JamlIde · controlled (parent owns jaml state)">
        <JamlIde
          jaml={controlledJaml}
          onChange={setControlledJaml}
          actions={
            <button
              onClick={() => setControlledJaml(SAMPLE_JAML)}
              style={{
                fontSize: 11, padding: "4px 10px",
                background: "#eac058", color: "#1e2b2d",
                border: "none", borderRadius: 4, cursor: "pointer",
              }}
            >
              Reset
            </button>
          }
        />
      </Section>

      <Section title="JamlIde · with mock search results + search button">
        <JamlIde
          defaultJaml={SAMPLE_JAML}
          searchResults={MOCK_RESULTS}
          onSearch={handleSearch}
          isSearching={isSearching}
        />
      </Section>
    </div>
  );
}
