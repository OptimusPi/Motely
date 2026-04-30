/// <reference types="vite/client" />
import React, { useEffect, useMemo, useState } from "react";
import {
  AnalyzerExplorer,
  CardFan,
  CardList,
  DeckSprite,
  JamlAnalyzerFullscreen,
  JamlAestheticSelector,
  JamlBoss,
  JamlCodeEditor,
  JamlGameCard,
  JamlIde,
  JamlMapEditor,
  JamlMapPreview,
  JamlSeedInput,
  JamlTag,
  JamlVoucher,
  JimboBackButton,
  JimboBackground,
  JimboBadge,
  JimboButton,
  JimboColorOption,
  JimboFlankNav,
  JimboFloating,
  JimboModal,
  JimboPanel,
  JimboTabs,
  JimboText,
  JimboToggleList,
  JimboTooltip,
  JimboVerticalTabs,
  JimboBalatroFooter,
  MotelyVersionBadge,
  RealStandardcard,
  Showcase,
  useAnalyzer,
  useSearch,
  type JamlAestheticOption,
  type JamlIdeSearchResult,
} from "../src/index.ts";
import { getMotelyWasmUrl } from "../src/assets.ts";
import "./App.css";

const C = JimboColorOption;

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
  - rareJoker: Baron
    score: 55
  - tag: NegativeTag
    antes: [1, 2]
    score: 60
  - joker: Any
    edition: Negative
    score: 40
mustNot:
  - boss: ThePlant
    antes: [8]
`;

// ─────────────────────────────────────────────────────────────────────────────
// Scenario registry
// ─────────────────────────────────────────────────────────────────────────────

// (ScenarioId and Scenario types moved to bottom with SCENARIOS registry)

// Boot motely once globally
import motely from "motely-wasm";
motely.boot().catch(console.error);

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: Analyzer (full-bleed, mobile snap-y mandatory — pifreak's #1 UX)
// ─────────────────────────────────────────────────────────────────────────────

function AnalyzerScenario() {
  const analyzer = useAnalyzer();

  useEffect(() => {
    analyzer.analyze("ALEPH999", "Red", "White", SAMPLE_JAML);
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  if (analyzer.status === "running" || analyzer.status === "idle") {
    return (
      <CenteredMessage>
        <JimboText size="md" tone="gold">Analyzing seed ALEPH999…</JimboText>
      </CenteredMessage>
    );
  }
  if (analyzer.error) {
    return (
      <CenteredMessage>
        <JimboText size="md" tone="red">Error: {analyzer.error}</JimboText>
      </CenteredMessage>
    );
  }
  return (
    <JamlAnalyzerFullscreen
      antes={analyzer.antes}
      live={analyzer.live}
      jaml={SAMPLE_JAML}
      tallyColumns={analyzer.tallyColumns[0]}
      tallyLabels={analyzer.tallyLabels}
    />
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: Live search — JAML IDE + properly-placed speedometer
// ─────────────────────────────────────────────────────────────────────────────

function SearchScenario() {
  const [jaml, setJaml] = useState(SAMPLE_JAML);
  const search = useSearch();

  const results: JamlIdeSearchResult[] = useMemo(
    () =>
      search.results.map((r) => ({
        seed: r.seed,
        score: r.score,
        tallyColumns: r.tallyColumns,
        tallyLabels: search.tallyLabels.length > 0 ? search.tallyLabels : undefined,
      })),
    [search.results, search.tallyLabels],
  );

  const isSearching = search.status === "running";
  const handleSearch = () => {
    if (isSearching) search.cancel();
    else search.start(jaml, 1_000_000);
  };

  return (
    <div className="jaml-demo-ide-scenario">
      {/* Speedometer banner — its own row, not crammed in a toolbar slot */}
      <div className="jaml-demo-speedometer-banner">
        <div className="jaml-demo-speedometer-status">
          <JimboText size="xs" tone="grey">Status</JimboText>
          <JimboText size="md" tone={isSearching ? "green" : "white"}>
            {search.status}
          </JimboText>
          {search.error && (
            <JimboText size="xs" tone="red">{search.error}</JimboText>
          )}
        </div>
        <JamlSpeedometer
          seedsPerSecond={search.seedsPerSecond}
          totalSearched={search.totalSearched}
          matchingSeeds={search.matchingSeeds}
          status={search.status}
        />
      </div>

      <JamlIde
        jaml={jaml}
        onChange={setJaml}
        searchResults={results}
        onSearch={handleSearch}
        isSearching={isSearching}
        className="jaml-demo-ide-component"
      />
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: JAML IDE (focused, just the IDE)
// ─────────────────────────────────────────────────────────────────────────────

function IdeScenario() {
  const [jaml, setJaml] = useState(SAMPLE_JAML);
  return <div className="jaml-demo-ide-container"><JamlIde jaml={jaml} onChange={setJaml} style={{ height: "100%" }} /></div>;
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: Code editor (raw)
// ─────────────────────────────────────────────────────────────────────────────

function CodeScenario() {
  const [jaml, setJaml] = useState(SAMPLE_JAML);
  return (
    <div className="jaml-demo-code-scenario">
      <JimboText size="xs" tone="grey">Raw JAML editor (no chrome)</JimboText>
      <div className="jaml-demo-code-editor-container">
        <JamlCodeEditor value={jaml} onChange={setJaml} minHeight={480} />
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: Map preview (visual JAML clauses)
// ─────────────────────────────────────────────────────────────────────────────

function MapScenario() {
  const [jaml, setJaml] = useState(SAMPLE_JAML);
  return (
    <div className="jaml-demo-map-scenario">
      <JamlMapPreview jaml={jaml} />
      <JamlCodeEditor value={jaml} onChange={setJaml} minHeight={200} />
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: JAML Map Visual Editor (NEW)
// ─────────────────────────────────────────────────────────────────────────────

function JamlMapEditorScenario() {
  return (
    <div className="jaml-demo-map-editor-scenario">
      <Section title="JAML Map Editor — Shop Slots">
        <JamlMapEditor />
      </Section>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: Card primitives
// ─────────────────────────────────────────────────────────────────────────────

function CardsScenario() {
  return (
    <div className="jaml-demo-cards-scenario">
      <Section title="JamlGameCard — joker">
        <JamlGameCard card={{ name: "Blueprint" }} type="joker" />
        <JamlGameCard card={{ name: "Perkeo", edition: "Negative" }} type="joker" />
        <JamlGameCard card={{ name: "Baron", edition: "Holographic" }} type="joker" />
      </Section>
      <Section title="JamlGameCard — consumable">
        <JamlGameCard card={{ name: "The Soul" }} type="consumable" />
        <JamlGameCard card={{ name: "Black Hole" }} type="consumable" />
      </Section>
      <Section title="JamlVoucher · JamlTag · JamlBoss">
        <JamlVoucher voucherName="Hieroglyph" />
        <JamlTag tagName="NegativeTag" />
        <JamlBoss bossName="ThePlant" />
      </Section>
      <Section title="RealStandardcard">
        <RealStandardcard suit="H" rank="A" />
        <RealStandardcard suit="S" rank="K" enhancement="Steel" seal="Red" />
        <RealStandardcard suit="D" rank="10" edition="Polychrome" />
      </Section>
      <CardList title="CardList" subtitle="horizontal scroll">
        <JamlGameCard card={{ name: "Blueprint" }} type="joker" />
        <JamlGameCard card={{ name: "Brainstorm" }} type="joker" />
        <JamlGameCard card={{ name: "Hologram" }} type="joker" />
        <JamlGameCard card={{ name: "Photograph" }} type="joker" />
      </CardList>
      <Section title="CardFan — 8 cards">
        <CardFan
          cards={["A_S", "K_H", "Q_D", "J_C", "10_S", "9_H", "8_D", "7_C"]}
          label="Starting hand"
        />
      </Section>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: Inputs (seed + aesthetic)
// ─────────────────────────────────────────────────────────────────────────────

function InputsScenario() {
  const [seed, setSeed] = useState("");
  const [aesthetic, setAesthetic] = useState<JamlAestheticOption | null>(null);
  return (
    <div className="j-flex-col j-gap-xl">
      <Section title="JamlSeedInput">
        <JamlSeedInput value={seed} onChange={setSeed} />
        <JimboText size="xs" tone="grey">value: {seed || "(empty)"}</JimboText>
      </Section>
      <Section title="JamlAestheticSelector">
        <JamlAestheticSelector value={aesthetic} onChange={(a) => setAesthetic(a)} />
        <JimboText size="xs" tone="grey">
          selected: {aesthetic ?? "(none)"}
        </JimboText>
      </Section>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: Decks + Stakes
// ─────────────────────────────────────────────────────────────────────────────

const DECKS = ["Red","Blue","Yellow","Green","Black","Magic","Nebula","Ghost",
  "Abandoned","Checkered","Zodiac","Painted","Anaglyph","Plasma","Erratic"];
const STAKES = ["White","Red","Green","Black","Blue","Purple","Orange","Gold"];

function DecksScenario() {
  return (
    <div className="j-flex-col j-gap-xl">
      <Section title="DeckSprite — every deck">
        <div className="jaml-demo-deck-list">
          {DECKS.map((d) => (
            <div key={d} className="jaml-demo-deck-item">
              <DeckSprite deck={d} size={56} />
              <JimboText size="xs" tone="grey">{d}</JimboText>
            </div>
          ))}
        </div>
      </Section>
      <Section title="Red deck × every stake">
        <div className="jaml-demo-deck-list">
          {STAKES.map((s) => (
            <div key={s} className="jaml-demo-deck-item">
              <DeckSprite deck="Red" stake={s} size={56} />
              <JimboText size="xs" tone="grey">{s}</JimboText>
            </div>
          ))}
        </div>
      </Section>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: Buttons
// ─────────────────────────────────────────────────────────────────────────────

function ButtonsScenario() {
  return (
    <div className="j-flex-col j-gap-xl">
      <Section title="JimboButton — every tone">
        <div className="j-flex j-flex-wrap j-gap-sm">
          {(["red","blue","green","orange"] as const).map((tone) => (
            <JimboButton key={tone} tone={tone} size="sm">{tone}</JimboButton>
          ))}
        </div>
      </Section>
      <Section title="JimboButton — every size">
        <div className="j-flex j-flex-wrap j-gap-sm j-items-center">
          {(["xs","sm","md","lg"] as const).map((size) => (
            <JimboButton key={size} tone="blue" size={size}>{size.toUpperCase()}</JimboButton>
          ))}
        </div>
      </Section>
      <Section title="fullWidth + disabled">
        <JimboButton tone="green" size="md" fullWidth>Full width green</JimboButton>
        <div className="j-gap-sm" />
        <JimboButton tone="red" size="sm" disabled>Disabled red</JimboButton>
      </Section>
      <Section title="JimboBackButton (full-width, anchored)">
        <JimboBackButton onClick={() => alert("Back tapped")} />
      </Section>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: Chrome (tabs, modal, tooltip, flank nav)
// ─────────────────────────────────────────────────────────────────────────────

function ChromeScenario() {
  const [tab, setTab] = useState("one");
  const [vtab, setVtab] = useState("alpha");
  const [modalOpen, setModalOpen] = useState(false);
  const [page, setPage] = useState(1);

  return (
    <div className="jaml-demo-chrome-container">
      <Section title="JimboTabs (horizontal)">
        <JimboTabs
          tabs={[
            { id: "one", label: "ONE" },
            { id: "two", label: "TWO" },
            { id: "three", label: "THREE" },
          ]}
          activeTab={tab}
          onTabChange={setTab}
        />
        <JimboText size="xs" tone="grey">active: {tab}</JimboText>
      </Section>
      <Section title="JimboVerticalTabs">
        <JimboVerticalTabs
          tabs={[
            { id: "alpha", label: "Alpha" },
            { id: "beta", label: "Beta" },
            { id: "gamma", label: "Gamma" },
          ]}
          activeTab={vtab}
          onTabChange={setVtab}
        />
        <JimboText size="xs" tone="grey">active: {vtab}</JimboText>
      </Section>
      <Section title="JimboTooltip">
        <JimboTooltip content="Blueprint copies the joker to its right.">
          <span className="jaml-demo-inline-block">
            <JamlGameCard card={{ name: "Blueprint" }} type="joker" />
          </span>
        </JimboTooltip>
      </Section>
      <Section title="JimboModal">
        <JimboButton tone="blue" size="sm" onClick={() => setModalOpen(true)}>Open modal</JimboButton>
        <JimboModal open={modalOpen} onClose={() => setModalOpen(false)} title="Sample Modal">
          <JimboText size="md">This is a JimboModal with a title and content.</JimboText>
        </JimboModal>
      </Section>
      <Section title="JimboFlankNav (prev / next)">
        <JimboFlankNav
          onPrev={() => setPage((p) => Math.max(1, p - 1))}
          onNext={() => setPage((p) => Math.min(8, p + 1))}
          canPrev={page > 1}
          canNext={page < 8}
        >
          <JimboText size="lg" tone="gold">Page {page} of 8</JimboText>
        </JimboFlankNav>
      </Section>
      <Section title="JimboPanel">
        <JimboPanel>
          <JimboText size="md">Inside a JimboPanel — drop shadows + the chunky Balatro frame.</JimboText>
        </JimboPanel>
      </Section>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: Primitives (Badges, Floating, ToggleList)
// ─────────────────────────────────────────────────────────────────────────────

function PrimitivesScenario() {
  const [toggles, setToggles] = useState([
    { id: 't1', label: 'Option A', on: true },
    { id: 't2', label: 'Option B', on: false },
    { id: 't3', label: 'Option C', on: false },
  ]);

  const onToggle = (id: string) => {
    setToggles(prev => prev.map(t => t.id === id ? { ...t, on: !t.on } : t));
  };

  return (
    <div className="jaml-demo-primitives-container">
      <Section title="JimboBadge">
        <div className="jaml-demo-flex-wrap">
          {(["dark","blue","red","green","orange","purple","gold","grey"] as const).map((tone) => (
            <JimboBadge key={tone} tone={tone} size="md">{tone}</JimboBadge>
          ))}
        </div>
      </Section>
      <Section title="JimboToggleList">
        <JimboToggleList
          title="Select Options"
          items={toggles}
          onToggle={onToggle}
        />
      </Section>
      <Section title="JimboFloating">
        <JimboPanel className="jaml-demo-floating-panel">
          <JimboText size="sm">A Panel with a floating badge</JimboText>
          <JimboFloating anchor="top-right" offset={8}>
            <JimboBadge tone="red" size="sm">New!</JimboBadge>
          </JimboFloating>
        </JimboPanel>
      </Section>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: Version badge + AnalyzerExplorer (legacy)
// ─────────────────────────────────────────────────────────────────────────────

function VersionScenario() {
  return (
    <div className="jaml-demo-version-container">
      <Section title="MotelyVersionBadge">
        <MotelyVersionBadge version="14.0.2" />
        <div className="jaml-demo-spacer" />
        <MotelyVersionBadge version="14.0.2" minimal />
        <div className="jaml-demo-spacer" />
        <MotelyVersionBadge loading />
      </Section>
      <Section title="AnalyzerExplorer">
        <AnalyzerExplorer
          jaml={SAMPLE_JAML}
          tallyColumns={[1, 0, 1, 0, 0, 1]}
          tallyLabels={["Blueprint", "Brainstorm", "Perkeo", "Baron", "NegativeTag", "Negative"]}
          antes={[
            {
              ante: 1,
              boss: "ThePlant",
              voucher: "Hieroglyph",
              smallBlindTag: "NegativeTag",
              bigBlindTag: "RareTag",
              packs: ["JumboArcanaPack", "MegaCelestialPack"],
              shop: [
                { id: "1-shop-0", name: "Blueprint", desired: true },
                { id: "1-shop-1", name: "Showman" },
                { id: "1-shop-2", name: "Perkeo", desired: true },
                { id: "1-shop-3", name: "The Hermit" },
              ],
            },
          ]}
          totalAntes={8}
        />
      </Section>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Consolidated: Components — all design-system primitives in one page
// ─────────────────────────────────────────────────────────────────────────────

function ComponentsScenario() {
  const [seed, setSeed] = useState("");
  const [aesthetic, setAesthetic] = useState<JamlAestheticOption | null>(null);
  const [tab, setTab] = useState("one");
  const [modalOpen, setModalOpen] = useState(false);
  const [page, setPage] = useState(1);
  const [toggles, setToggles] = useState([
    { id: 't1', label: 'Option A', on: true },
    { id: 't2', label: 'Option B', on: false },
    { id: 't3', label: 'Option C', on: false },
  ]);

  return (
    <div className="jaml-demo-scenario">
      {/* Buttons */}
      <Section title="JimboButton — every tone">
        <div className="j-flex j-flex-wrap j-gap-sm">
          {(["red","blue","green","gold","orange","grey"] as const).map((tone) => (
            <JimboButton key={tone} tone={tone} size="sm">{tone}</JimboButton>
          ))}
        </div>
      </Section>
      <Section title="JimboButton — every size">
        <div className="j-flex j-flex-wrap j-gap-sm j-items-center">
          {(["xs","sm","md","lg"] as const).map((size) => (
            <JimboButton key={size} tone="blue" size={size}>{size.toUpperCase()}</JimboButton>
          ))}
        </div>
      </Section>
      <Section title="fullWidth + disabled">
        <JimboButton tone="green" size="md" fullWidth>Full width green</JimboButton>
        <div className="j-gap-sm" />
        <JimboButton tone="red" size="sm" disabled>Disabled red</JimboButton>
      </Section>

      {/* Badges */}
      <Section title="JimboBadge">
        <div className="jaml-demo-flex-wrap">
          {(["dark","blue","red","green","orange","purple","gold","grey"] as const).map((tone) => (
            <JimboBadge key={tone} tone={tone} size="md">{tone}</JimboBadge>
          ))}
        </div>
      </Section>

      {/* Inputs */}
      <Section title="JamlSeedInput">
        <JamlSeedInput value={seed} onChange={setSeed} />
        <JimboText size="xs" tone="grey">value: {seed || "(empty)"}</JimboText>
      </Section>
      <Section title="JamlAestheticSelector">
        <JamlAestheticSelector value={aesthetic} onChange={(a) => setAesthetic(a)} />
      </Section>

      {/* Tabs */}
      <Section title="JimboTabs">
        <JimboTabs
          tabs={[
            { id: "one", label: "ONE" },
            { id: "two", label: "TWO" },
            { id: "three", label: "THREE" },
          ]}
          activeTab={tab}
          onTabChange={setTab}
        />
      </Section>

      {/* Modal */}
      <Section title="JimboModal">
        <JimboButton tone="blue" size="sm" onClick={() => setModalOpen(true)}>Open modal</JimboButton>
        <JimboModal open={modalOpen} onClose={() => setModalOpen(false)} title="Sample Modal">
          <JimboText size="md">This is a JimboModal with a title and content.</JimboText>
        </JimboModal>
      </Section>

      {/* Tooltip */}
      <Section title="JimboTooltip">
        <JimboTooltip content="Blueprint copies the joker to its right.">
          <span className="jaml-demo-inline-block">
            <JamlGameCard card={{ name: "Blueprint" }} type="joker" />
          </span>
        </JimboTooltip>
      </Section>

      {/* FlankNav */}
      <Section title="JimboFlankNav">
        <JimboFlankNav
          onPrev={() => setPage((p) => Math.max(1, p - 1))}
          onNext={() => setPage((p) => Math.min(8, p + 1))}
          canPrev={page > 1}
          canNext={page < 8}
        >
          <JimboText size="lg" tone="gold">Page {page} of 8</JimboText>
        </JimboFlankNav>
      </Section>

      {/* Toggle list */}
      <Section title="JimboToggleList">
        <JimboToggleList
          title="Select Options"
          items={toggles}
          onToggle={(id) => setToggles(prev => prev.map(t => t.id === id ? { ...t, on: !t.on } : t))}
        />
      </Section>

      {/* Floating badge */}
      <Section title="JimboFloating">
        <JimboPanel className="jaml-demo-floating-panel">
          <JimboText size="sm">A Panel with a floating badge</JimboText>
          <JimboFloating anchor="top-right" offset={8}>
            <JimboBadge tone="red" size="sm">New!</JimboBadge>
          </JimboFloating>
        </JimboPanel>
      </Section>

      {/* Cards */}
      <Section title="JamlGameCard">
        <JamlGameCard card={{ name: "Blueprint" }} type="joker" />
        <JamlGameCard card={{ name: "Perkeo", edition: "Negative" }} type="joker" />
        <JamlGameCard card={{ name: "The Soul" }} type="consumable" />
      </Section>
      <Section title="JamlVoucher · JamlTag · JamlBoss">
        <JamlVoucher voucherName="Hieroglyph" />
        <JamlTag tagName="NegativeTag" />
        <JamlBoss bossName="ThePlant" />
      </Section>

      {/* CardFan */}
      <Section title="CardFan">
        <CardFan
          cards={["A_S", "K_H", "Q_D", "J_C", "10_S", "9_H", "8_D", "7_C"]}
          label="Starting hand"
        />
      </Section>

      {/* Decks */}
      <Section title="DeckSprite — every deck">
        <div className="jaml-demo-deck-list">
          {["Red","Blue","Yellow","Green","Black","Magic","Nebula","Ghost",
            "Abandoned","Checkered","Zodiac","Painted","Anaglyph","Plasma","Erratic"].map((d) => (
            <div key={d} className="jaml-demo-deck-item">
              <DeckSprite deck={d} size={56} />
              <JimboText size="xs" tone="grey">{d}</JimboText>
            </div>
          ))}
        </div>
      </Section>

      {/* Version badge */}
      <Section title="MotelyVersionBadge">
        <MotelyVersionBadge version="14.0.2" />
        <div className="jaml-demo-spacer" />
        <MotelyVersionBadge version="14.0.2" minimal />
      </Section>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// JAML Map — search results (stub — design ref: assets/...DesignsV2/src/v2/SearchResultsV2.jsx)
// ─────────────────────────────────────────────────────────────────────────────

function JamlMapScenario() {
  return (
    <div className="jaml-demo-scenario">
      <JimboPanel>
        <JimboText size="lg" tone="gold">JAML Map</JimboText>
        <JimboText size="sm" tone="grey">
          Search results table — tap row to expand into full seed detail.
        </JimboText>
        <JimboText size="xs" tone="grey">
          Design ref: SearchResultsV2.jsx · SeedDetailV2.jsx
        </JimboText>
      </JimboPanel>
      <JimboPanel>
        <JimboText size="xs" tone="grey">Coming soon — pifreak will help with the design.</JimboText>
      </JimboPanel>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// OG — social card preview (stub — design ref: assets/...DesignsV2/src/v2/SeedOGCard.jsx)
// ─────────────────────────────────────────────────────────────────────────────

function OGScenario() {
  return (
    <div style={{ width: '100%', height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#0b1416' }}>
      <div style={{
        width: 600, height: 315,
        background: 'radial-gradient(ellipse at top left, #2d4a38 0%, #0f1a13 70%)',
        borderRadius: 8,
        border: `2px solid ${C.PANEL_EDGE}`,
        display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
        fontFamily: 'm6x11plus, monospace',
      }}>
        <JimboText size="lg" tone="gold">OG Card Preview</JimboText>
        <JimboText size="sm" tone="grey">1200×630 · Vercel OG spec</JimboText>
        <JimboText size="xs" tone="grey">Design ref: SeedOGCard.jsx</JimboText>
        <div style={{ marginTop: 16 }}>
          <JimboText size="xs" tone="grey">Renders: seed + score + fanned joker hand</JimboText>
        </div>
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────────────

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="jaml-demo-section">
      <div className="jaml-demo-section-title">{title}</div>
      <div className="jaml-demo-section-body">
        {children}
      </div>
    </div>
  );
}

function CenteredMessage({ children }: { children: React.ReactNode }) {
  return (
    <div className="jaml-demo-centered-message">
      {children}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Combo IDE — tabbed power view (IDE + Visual all in one)
// ─────────────────────────────────────────────────────────────────────────────

function ComboIdeScenario() {
  const [jaml, setJaml] = useState(SAMPLE_JAML);
  const [activeTab, setActiveTab] = useState("editor");
  const search = useSearch();

  const results: JamlIdeSearchResult[] = useMemo(
    () =>
      search.results.map((r) => ({
        seed: r.seed,
        score: r.score,
        tallyColumns: r.tallyColumns,
        tallyLabels: search.tallyLabels.length > 0 ? search.tallyLabels : undefined,
      })),
    [search.results, search.tallyLabels],
  );

  const isSearching = search.status === "running";
  const handleSearch = () => {
    if (isSearching) search.cancel();
    else search.start(jaml, 1_000_000);
  };

  return (
    <div className="jaml-demo-scenario" style={{ gap: 0 }}>
      <JimboTabs
        tabs={[
          { id: "editor", label: "EDITOR" },
          { id: "visual", label: "VISUAL" },
        ]}
        activeTab={activeTab}
        onTabChange={setActiveTab}
      />
      <div style={{ flex: 1, minHeight: 0, overflow: 'auto' }}>
        {activeTab === "editor" && (
          <JamlIde
            jaml={jaml}
            onChange={setJaml}
            searchResults={results}
            onSearch={handleSearch}
            isSearching={isSearching}
            style={{ height: "100%" }}
          />
        )}
        {activeTab === "visual" && (
          <div style={{ padding: 12 }}>
            <JamlMapEditor />
          </div>
        )}
      </div>
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// JAML Visual — drag-and-drop builder (stub)
// ─────────────────────────────────────────────────────────────────────────────

function VisualScenario() {
  return (
    <div className="jaml-demo-scenario" style={{ padding: 12 }}>
      <JamlMapEditor />
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main — 7 scenarios
// ─────────────────────────────────────────────────────────────────────────────

type ScenarioId = "showcase" | "combo" | "ide" | "map" | "mapeditor" | "visual" | "analyzer" | "og";

interface Scenario {
  id: ScenarioId;
  label: string;
  render: () => React.ReactElement;
}

const SCENARIOS: Scenario[] = [
  { id: "showcase",  label: "Lib Showcase",    render: () => <ComponentsScenario /> },
  { id: "combo",     label: "Combo IDE",       render: () => <ComboIdeScenario /> },
  { id: "ide",       label: "JAML IDE",        render: () => <IdeScenario /> },
  { id: "map",       label: "JAML Map",        render: () => <JamlMapScenario /> },
  { id: "mapeditor", label: "Map Editor",      render: () => <JamlMapEditorScenario /> },
  { id: "visual",    label: "JAML Visual",     render: () => <VisualScenario /> },
  { id: "analyzer",  label: "Seed Analyzer",   render: () => <AnalyzerScenario /> },
  { id: "og",        label: "OG Card",         render: () => <OGScenario /> },
];

export function App() {
  const [current, setCurrent] = useState<ScenarioId>(() => {
    const p = new URLSearchParams(window.location.search).get("scenario");
    return SCENARIOS.some((s) => s.id === p) ? (p as ScenarioId) : "showcase";
  });
  const scenario = SCENARIOS.find((s) => s.id === current) ?? SCENARIOS[0];

  return (
      <div className="jaml-demo-root">
      <JimboBackground />

      {/* Sticky top nav */}
      <header className="jaml-demo-header">
        <span className="jaml-demo-wordmark">JAML-UI</span>
        <select
          value={current}
          onChange={(e) => setCurrent(e.target.value as ScenarioId)}
          className="jaml-demo-select"
          aria-label="Pick scenario"
        >
          {SCENARIOS.map((s) => (
            <option key={s.id} value={s.id}>{s.label}</option>
          ))}
        </select>
      </header>

      <div className="jaml-demo-content-wrapper">
        <div className="jaml-demo-layout">
          <main className="jaml-demo-main">
            <div className="jaml-demo-scenario">
              {scenario.render()}
            </div>
          </main>
        </div>
      </div>

      {/* Sticky bottom footer */}
      <div className="jaml-demo-footer">
        <JimboBalatroFooter />
      </div>
    </div>
  );
}
