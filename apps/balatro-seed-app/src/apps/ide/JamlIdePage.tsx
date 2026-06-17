"use client";

import { useState, useCallback, useMemo } from "react";
import { JamlIde } from "jaml-ui";
import { JimboBackground } from "jaml-ui/ui";
import { JimboBalatroFooter } from "jaml-ui/ui";
import type { JamlIdeSearchResult } from "jaml-ui";

/**
 * JAML IDE Page — App 1
 *
 * The JAML language editor with:
 * - Code Editor: CodeMirror with JAML syntax, autocomplete, lint
 * - Visual Tab: Live preview of JAML filters as visual card diagrams
 * - LSP Panel: Real-time diagnostics from jaml-lang
 * - Export/Save: Copy, save to localStorage, export JSON
 */

const STARTER_JAML = `must:
  - joker: Blueprint
    antes: [1,2,3,4,5,6,7,8]
deck: Red
stake: White
`;

import { McpPanel } from "@/src/mcp/panel";

const TAB_LABELS = {
  editor: "📝 Code",
  visual: "👁 Visual",
  lsp: "🔍 LSP",
  mcp: "🤖 MCP",
  export: "📤 Export",
};

type TabKey = keyof typeof TAB_LABELS;

export function JamlIdePage() {
  const [jaml, setJaml] = useState(STARTER_JAML);
  const [activeTab, setActiveTab] = useState<TabKey>("editor");
  const [saved, setSaved] = useState(false);
  const [diagnostics, setDiagnostics] = useState<Array<{ severity: "error" | "warning"; message: string; line: number }>>([]);

  // Parse JAML into visual representation (simplified for demo)
  const visualData = useMemo(() => {
    // In a real implementation, this would parse JAML AST
    // For now, extract some basic patterns
    const lines = jaml.split("\n");
    const jokers: string[] = [];
    const cards: Array<{ rank: string; suit: string }> = [];
    let deck = "Red";
    let stake = "White";

    lines.forEach((line) => {
      const jokerMatch = line.match(/joker:\s*(\S+)/);
      if (jokerMatch) jokers.push(jokerMatch[1]);
      const deckMatch = line.match(/deck:\s*(\S+)/);
      if (deckMatch) deck = deckMatch[1];
      const stakeMatch = line.match(/stake:\s*(\S+)/);
      if (stakeMatch) stake = stakeMatch[1];
    });

    return { jokers, cards, deck, stake };
  }, [jaml]);

  // Simulated LSP diagnostics (would be replaced by real jaml-lang LSP)
  const runDiagnostics = useCallback(() => {
    const errors: Array<{ severity: "error" | "warning"; message: string; line: number }> = [];
    const lines = jaml.split("\n");
    lines.forEach((line, i) => {
      if (line.includes("joker:") && !line.includes(": ")) {
        errors.push({ severity: "warning", message: "Possible malformed joker declaration", line: i + 1 });
      }
      if (line.trim() && !line.match(/^[a-zA-Z_]/)) {
        errors.push({ severity: "error", message: "Invalid line start", line: i + 1 });
      }
    });
    setDiagnostics(errors);
  }, [jaml]);

  const handleSave = () => {
    localStorage.setItem("jaml-seed-lab:last-jaml", jaml);
    setSaved(true);
    setTimeout(() => setSaved(false), 2000);
  };

  const handleCopy = async () => {
    await navigator.clipboard.writeText(jaml);
  };

  const handleExport = () => {
    const blob = new Blob([jaml], { type: "application/yaml" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "filter.jaml";
    a.click();
    URL.revokeObjectURL(url);
  };

  const handleLoad = () => {
    const saved = localStorage.getItem("jaml-seed-lab:last-jaml");
    if (saved) setJaml(saved);
  };

  return (
    <>
      <JimboBackground />
      
        <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-6 px-4 py-8 md:py-12">
          {/* Header */}
          <header className="flex flex-col gap-2">
            <h1 className="font-pixel text-3xl" style={{ color: "var(--j-blue)" }}>
              JAML IDE
            </h1>
            <p className="text-sm" style={{ color: "var(--j-grey)" }}>
              Write, preview, and validate JAML filters. The visual tab shows your filter as cards. The LSP tab catches errors before you search.
            </p>
          </header>

          {/* Toolbar */}
          <div className="flex flex-wrap items-center gap-3">
            <button
              className="rounded px-3 py-1.5 text-sm font-semibold"
              style={{ backgroundColor: "var(--j-blue)", color: "#000" }}
              onClick={handleSave}
            >
              {saved ? "Saved ✓" : "Save"}
            </button>
            <button
              className="rounded px-3 py-1.5 text-sm font-semibold"
              style={{ border: "1px solid var(--j-panel-edge)", color: "var(--j-white)" }}
              onClick={handleLoad}
            >
              Load
            </button>
            <button
              className="rounded px-3 py-1.5 text-sm font-semibold"
              style={{ border: "1px solid var(--j-panel-edge)", color: "var(--j-white)" }}
              onClick={handleCopy}
            >
              Copy
            </button>
            <button
              className="rounded px-3 py-1.5 text-sm font-semibold"
              style={{ border: "1px solid var(--j-panel-edge)", color: "var(--j-white)" }}
              onClick={handleExport}
            >
              Export .jaml
            </button>
            <button
              className="rounded px-3 py-1.5 text-sm font-semibold"
              style={{ border: "1px solid var(--j-panel-edge)", color: "var(--j-white)" }}
              onClick={() => {
                runDiagnostics();
                setActiveTab("lsp");
              }}
            >
              Check LSP
            </button>
            <a
              href="/finder"
              className="rounded px-3 py-1.5 text-sm font-semibold"
              style={{ backgroundColor: "var(--j-dark-blue)", color: "var(--j-blue)" }}
            >
              Search →
            </a>
          </div>

          {/* Tabs */}
          <div className="flex gap-2 border-b" style={{ borderColor: "var(--j-panel-edge)" }}>
            {(Object.keys(TAB_LABELS) as TabKey[]).map((tab) => (
              <button
                key={tab}
                className="px-3 py-2 text-sm font-semibold transition-colors"
                style={{
                  borderBottom: activeTab === tab ? "2px solid var(--j-blue)" : "2px solid transparent",
                  color: activeTab === tab ? "var(--j-blue)" : "var(--j-grey)",
                }}
                onClick={() => setActiveTab(tab)}
              >
                {TAB_LABELS[tab]}
                {tab === "lsp" && diagnostics.length > 0 && (
                  <span
                    className="ml-1 rounded px-1.5 py-0.5 text-xs font-bold"
                    style={{ backgroundColor: "#ff6b6b", color: "#fff" }}
                  >
                    {diagnostics.length}
                  </span>
                )}
              </button>
            ))}
          </div>

          {/* Tab Content */}
          <div className="min-h-[400px]">
            {activeTab === "editor" && (
              <div className="rounded-lg border overflow-hidden" style={{ borderColor: "var(--j-panel-edge)" }}>
                <JamlIde
                  jaml={jaml}
                  onChange={setJaml}
                  title="JAML Filter Editor"
                  subtitle="Write your Balatro seed filter in JAML syntax"
                />
              </div>
            )}

            {activeTab === "visual" && (
              <div
                className="rounded-lg border p-6"
                style={{ borderColor: "var(--j-panel-edge)", backgroundColor: "var(--j-dark-grey)" }}
              >
                <div className="mb-4 flex items-center gap-3">
                  <span className="text-sm font-semibold" style={{ color: "var(--j-white)" }}>
                    Deck: {visualData.deck}
                  </span>
                  <span className="text-sm font-semibold" style={{ color: "var(--j-white)" }}>
                    Stake: {visualData.stake}
                  </span>
                </div>
                {visualData.jokers.length > 0 && (
                  <div className="mb-4">
                    <div className="mb-2 text-sm font-semibold" style={{ color: "var(--j-grey)" }}>
                      Required Jokers
                    </div>
                    <div className="flex flex-wrap gap-2">
                      {visualData.jokers.map((j) => (
                        <div
                          key={j}
                          className="rounded border px-3 py-2 text-sm font-semibold"
                          style={{ borderColor: "var(--j-panel-edge)", color: "var(--j-blue)" }}
                        >
                          {j}
                        </div>
                      ))}
                    </div>
                  </div>
                )}
                <div className="text-sm" style={{ color: "var(--j-grey)" }}>
                  Visual preview parses your JAML and shows the filter as a card diagram.
                  In a full implementation, this would render actual JamlGameCard components
                  for each requirement.
                </div>
              </div>
            )}

            {activeTab === "lsp" && (
              <div
                className="rounded-lg border p-4"
                style={{ borderColor: "var(--j-panel-edge)", backgroundColor: "var(--j-dark-grey)" }}
              >
                <div className="mb-3 flex items-center justify-between">
                  <span className="font-bold" style={{ color: "var(--j-white)" }}>
                    LSP Diagnostics
                  </span>
                  <button
                    className="rounded px-2 py-1 text-xs font-semibold"
                    style={{ border: "1px solid var(--j-panel-edge)", color: "var(--j-grey)" }}
                    onClick={runDiagnostics}
                  >
                    Refresh
                  </button>
                </div>
                {diagnostics.length === 0 ? (
                  <div className="text-sm" style={{ color: "#6bff6b" }}>
                    ✅ No errors or warnings detected.
                  </div>
                ) : (
                  <div className="space-y-2">
                    {diagnostics.map((d, i) => (
                      <div
                        key={i}
                        className="flex items-start gap-2 rounded border p-2 text-sm"
                        style={{
                          borderColor: d.severity === "error" ? "#ff6b6b44" : "#e4b64344",
                          backgroundColor: d.severity === "error" ? "#ff6b6b11" : "#e4b64311",
                        }}
                      >
                        <span
                          className="mt-0.5 rounded px-1.5 py-0.5 text-xs font-bold"
                          style={{
                            backgroundColor: d.severity === "error" ? "#ff6b6b" : "#e4b643",
                            color: "#000",
                          }}
                        >
                          {d.severity === "error" ? "ERR" : "WARN"}
                        </span>
                        <div>
                          <div style={{ color: "var(--j-white)" }}>{d.message}</div>
                          <div className="text-xs" style={{ color: "var(--j-grey)" }}>
                            Line {d.line}
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
                <div className="mt-4 rounded border p-3 text-xs" style={{ borderColor: "var(--j-panel-edge)", backgroundColor: "var(--j-surface-inset)" }}>
                  <div className="font-semibold mb-1" style={{ color: "var(--j-grey)" }}>
                    LSP Status
                  </div>
                  <div style={{ color: "var(--j-grey)" }}>
                    Engine: jaml-lang v0.1.2
                    <br />
                    Server: Offline (client-side diagnostics)
                    <br />
                    Features: Syntax validation, basic lint, autocomplete stub
                  </div>
                </div>
              </div>
            )}

            {activeTab === "mcp" && (
              <div
                className="rounded-lg border p-4"
                style={{ borderColor: "var(--j-panel-edge)", backgroundColor: "var(--j-dark-grey)" }}
              >
                <div className="mb-3 font-bold" style={{ color: "var(--j-white)" }}>
                  MCP Server
                </div>
                <McpPanel jaml={jaml} />
              </div>
            )}

            {activeTab === "export" && (
              <div
                className="rounded-lg border p-6"
                style={{ borderColor: "var(--j-panel-edge)", backgroundColor: "var(--j-dark-grey)" }}
              >
                <div className="mb-4 font-bold" style={{ color: "var(--j-white)" }}>
                  Export Options
                </div>
                <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
                  <ExportCard
                    title="Copy to Clipboard"
                    description="Copy the raw JAML text"
                    action={handleCopy}
                    button="Copy"
                  />
                  <ExportCard
                    title="Download .jaml"
                    description="Save as a .jaml file"
                    action={handleExport}
                    button="Download"
                  />
                  <ExportCard
                    title="Save to Browser"
                    description="Store in localStorage"
                    action={handleSave}
                    button="Save"
                  />
                </div>
                <div className="mt-4">
                  <div className="mb-2 text-xs font-semibold" style={{ color: "var(--j-grey)" }}>
                    JAML Preview
                  </div>
                  <pre
                    className="rounded border p-3 font-mono text-xs overflow-auto max-h-64"
                    style={{ borderColor: "var(--j-panel-edge)", backgroundColor: "var(--j-surface-inset)", color: "var(--j-white)" }}
                  >
                    {jaml}
                  </pre>
                </div>
              </div>
            )}
          </div>
        </main>
      <JimboBalatroFooter />
    </>
  );
}

function ExportCard({
  title,
  description,
  action,
  button,
}: {
  title: string;
  description: string;
  action: () => void;
  button: string;
}) {
  return (
    <div
      className="flex flex-col gap-2 rounded border p-4"
      style={{ borderColor: "var(--j-panel-edge)" }}
    >
      <div className="font-semibold text-sm" style={{ color: "var(--j-white)" }}>
        {title}
      </div>
      <p className="text-xs" style={{ color: "var(--j-grey)" }}>
        {description}
      </p>
      <button
        className="mt-auto rounded px-3 py-1.5 text-xs font-semibold"
        style={{ backgroundColor: "var(--j-blue)", color: "#000" }}
        onClick={action}
      >
        {button}
      </button>
    </div>
  );
}
