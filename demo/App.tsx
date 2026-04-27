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
  JamlMapPreview,
  JamlSeedInput,
  JamlSpeedometer,
  JamlTag,
  JamlVoucher,
  MotelyVersionBadge,
  RealStandardcard,
  useAnalyzer,
  useSearch,
  type JamlAestheticOption,
  type JamlIdeSearchResult,
} from "jaml-ui";
import {
  JimboBackButton,
  JimboBackground,
  JimboButton,
  JimboColorOption,
  JimboFlankNav,
  JimboModal,
  JimboPanel,
  JimboTabs,
  JimboText,
  JimboTooltip,
  JimboVerticalTabs,
} from "jaml-ui/ui";
const C = JimboColorOption;
const MOTELY_WASM_URL = `${import.meta.env.VITE_CDN_BASE_URL}/motely-wasm/${import.meta.env.VITE_MOTELY_WASM_VERSION}/index.mjs`;

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

type ScenarioId =
  | "analyzer"
  | "search"
  | "ide"
  | "code"
  | "map"
  | "cards"
  | "inputs"
  | "decks"
  | "buttons"
  | "chrome"
  | "version";

interface Scenario {
  id: ScenarioId;
  label: string;
  fullBleed?: boolean;
  render: () => React.ReactElement;
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: Analyzer (full-bleed, mobile snap-y mandatory — pifreak's #1 UX)
// ─────────────────────────────────────────────────────────────────────────────

function AnalyzerScenario() {
  const analyzer = useAnalyzer(MOTELY_WASM_URL);

  useEffect(() => {
    analyzer.analyze("ALEPH999", "Red", "White", SAMPLE_JAML);
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  if (analyzer.status === "running" || analyzer.status === "idle") {
    return (
      <CenteredMessage>
        <JimboText size="md" tone="gold">Booting motely-wasm + analyzing seed ALEPH999…</JimboText>
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
  return <JamlAnalyzerFullscreen antes={analyzer.antes} live={analyzer.live} />;
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: Live search — JAML IDE + properly-placed speedometer
// ─────────────────────────────────────────────────────────────────────────────

function SearchScenario() {
  const [jaml, setJaml] = useState(SAMPLE_JAML);
  const search = useSearch(MOTELY_WASM_URL);

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
    <div style={{ display: "flex", flexDirection: "column", gap: 10, height: "100%" }}>
      {/* Speedometer banner — its own row, not crammed in a toolbar slot */}
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 12,
          padding: 10,
          background: C.DARK_GREY,
          border: `1px solid ${C.PANEL_EDGE}`,
          borderRadius: 6,
          flexWrap: "wrap",
        }}
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 4, flex: "1 1 200px" }}>
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
        style={{ flex: 1, minHeight: 380 }}
      />
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: JAML IDE (focused, just the IDE)
// ─────────────────────────────────────────────────────────────────────────────

function IdeScenario() {
  const [jaml, setJaml] = useState(SAMPLE_JAML);
  return <JamlIde jaml={jaml} onChange={setJaml} style={{ height: "100%", minHeight: 480 }} />;
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: Code editor (raw)
// ─────────────────────────────────────────────────────────────────────────────

function CodeScenario() {
  const [jaml, setJaml] = useState(SAMPLE_JAML);
  return (
    <div style={{ height: "100%", minHeight: 480, display: "flex", flexDirection: "column" }}>
      <JimboText size="xs" tone="grey">Raw JAML editor (no chrome)</JimboText>
      <div style={{ flex: 1, marginTop: 6 }}>
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
    <div style={{ display: "flex", flexDirection: "column", gap: 10, height: "100%" }}>
      <JamlMapPreview jaml={jaml} />
      <JamlCodeEditor value={jaml} onChange={setJaml} minHeight={200} />
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Scenario: Card primitives
// ─────────────────────────────────────────────────────────────────────────────

function CardsScenario() {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
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
    <div style={{ display: "flex", flexDirection: "column", gap: 16, maxWidth: 360 }}>
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
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <Section title="DeckSprite — every deck">
        <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
          {DECKS.map((d) => (
            <div key={d} style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 2 }}>
              <DeckSprite deck={d} size={56} />
              <JimboText size="xs" tone="grey">{d}</JimboText>
            </div>
          ))}
        </div>
      </Section>
      <Section title="Red deck × every stake">
        <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
          {STAKES.map((s) => (
            <div key={s} style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 2 }}>
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
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <Section title="JimboButton — every tone">
        <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
          {(["red","blue","green","gold","orange","grey"] as const).map((tone) => (
            <JimboButton key={tone} tone={tone} size="sm">{tone}</JimboButton>
          ))}
        </div>
      </Section>
      <Section title="JimboButton — every size">
        <div style={{ display: "flex", flexWrap: "wrap", gap: 6, alignItems: "center" }}>
          {(["xs","sm","md","lg"] as const).map((size) => (
            <JimboButton key={size} tone="blue" size={size}>{size.toUpperCase()}</JimboButton>
          ))}
        </div>
      </Section>
      <Section title="fullWidth + disabled">
        <JimboButton tone="green" size="md" fullWidth>Full width green</JimboButton>
        <div style={{ height: 6 }} />
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
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
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
          <span style={{ display: "inline-block" }}>
            <JamlGameCard card={{ name: "Blueprint" }} type="joker" />
          </span>
        </JimboTooltip>
      </Section>
      <Section title="JimboModal">
        <JimboButton tone="blue" size="sm" onClick={() => setModalOpen(true)}>Open modal</JimboButton>
        <JimboModal open={modalOpen} onClose={() => setModalOpen(false)} title="Sample Modal">
          <JimboText size="md">This is a JimboModal with a title and content.</JimboText>
          <div style={{ height: 8 }} />
          <JimboButton tone="orange" size="sm" onClick={() => setModalOpen(false)}>Close</JimboButton>
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
// Scenario: Version badge + AnalyzerExplorer (legacy)
// ─────────────────────────────────────────────────────────────────────────────

function VersionScenario() {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <Section title="MotelyVersionBadge">
        <MotelyVersionBadge version="13.0.3" />
        <div style={{ height: 6 }} />
        <MotelyVersionBadge version="13.0.3" minimal />
        <div style={{ height: 6 }} />
        <MotelyVersionBadge loading />
      </Section>
      <Section title="AnalyzerExplorer (legacy, kept for back-compat)">
        <AnalyzerExplorer
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
// Helpers
// ─────────────────────────────────────────────────────────────────────────────

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <JimboText size="xs" tone="grey">{title}</JimboText>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 8, alignItems: "flex-start" }}>
        {children}
      </div>
    </div>
  );
}

function CenteredMessage({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        height: "100%",
        minHeight: 320,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
      }}
    >
      {children}
    </div>
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// Main
// ─────────────────────────────────────────────────────────────────────────────

const SCENARIOS: Scenario[] = [
  { id: "analyzer", label: "Analyzer (full)", fullBleed: true, render: () => <AnalyzerScenario /> },
  { id: "search", label: "Search", render: () => <SearchScenario /> },
  { id: "ide", label: "JAML IDE", render: () => <IdeScenario /> },
  { id: "code", label: "Code editor", render: () => <CodeScenario /> },
  { id: "map", label: "Map preview", render: () => <MapScenario /> },
  { id: "cards", label: "Cards", render: () => <CardsScenario /> },
  { id: "inputs", label: "Inputs", render: () => <InputsScenario /> },
  { id: "decks", label: "Decks + stakes", render: () => <DecksScenario /> },
  { id: "buttons", label: "Buttons", render: () => <ButtonsScenario /> },
  { id: "chrome", label: "Tabs / Modal / Tooltip", render: () => <ChromeScenario /> },
  { id: "version", label: "Version badge + legacy", render: () => <VersionScenario /> },
];

export function App() {
  const [current, setCurrent] = useState<ScenarioId>("analyzer");
  const scenario = SCENARIOS.find((s) => s.id === current) ?? SCENARIOS[0];
  const fullBleed = scenario.fullBleed === true;

  return (
    <div
      style={{
        position: "relative",
        minHeight: "100svh",
        color: C.WHITE,
        fontFamily: "var(--font-sans, m6x11plus), monospace",
      }}
    >
      <JimboBackground />

      {fullBleed ? (
        <>
          {scenario.render()}
          {/* Floating exit chip — top-left so it doesn't fight with the analyzer's side rail */}
          <div
            style={{
              position: "fixed",
              top: 8,
              left: 8,
              zIndex: 10,
              display: "flex",
              gap: 6,
              alignItems: "center",
              background: "rgba(0,0,0,0.65)",
              padding: "4px 8px",
              borderRadius: 6,
              backdropFilter: "blur(4px)",
            }}
          >
            <select
              value={current}
              onChange={(e) => setCurrent(e.target.value as ScenarioId)}
              style={selectStyle}
            >
              {SCENARIOS.map((s) => (
                <option key={s.id} value={s.id}>{s.label}</option>
              ))}
            </select>
          </div>
        </>
      ) : (
        <div style={{ position: "relative", zIndex: 1, display: "flex", flexDirection: "column", minHeight: "100svh" }}>
          {/* Sticky top bar — brand + scenario picker. Mobile-first single column. */}
          <header
            style={{
              position: "sticky",
              top: 0,
              zIndex: 5,
              display: "flex",
              gap: 10,
              alignItems: "center",
              justifyContent: "space-between",
              padding: "10px 12px",
              background: "rgba(0,0,0,0.65)",
              backdropFilter: "blur(6px)",
              borderBottom: `1px solid ${C.PANEL_EDGE}`,
            }}
          >
            <div style={{ display: "flex", flexDirection: "column" }}>
              <span style={{ color: C.GOLD, fontSize: 10, letterSpacing: 3 }}>JAML-UI</span>
              <span style={{ color: C.WHITE, fontSize: 13 }}>component demo</span>
            </div>
            <select
              value={current}
              onChange={(e) => setCurrent(e.target.value as ScenarioId)}
              style={selectStyle}
              aria-label="Pick scenario"
            >
              {SCENARIOS.map((s) => (
                <option key={s.id} value={s.id}>{s.label}</option>
              ))}
            </select>
          </header>

          <main
            style={{
              flex: 1,
              padding: 12,
              maxWidth: 1100,
              width: "100%",
              boxSizing: "border-box",
              margin: "0 auto",
              display: "flex",
              flexDirection: "column",
            }}
          >
            {scenario.render()}
          </main>
        </div>
      )}
    </div>
  );
}

const selectStyle: React.CSSProperties = {
  padding: "6px 10px",
  background: C.DARKEST,
  color: C.GOLD,
  border: `1px solid ${C.PANEL_EDGE}`,
  borderRadius: 4,
  fontSize: 13,
  fontFamily: "inherit",
  cursor: "pointer",
  minWidth: 160,
};
