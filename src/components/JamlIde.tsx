"use client";

import React, { useMemo, useState } from "react";
import { JamlMapPreview } from "./JamlMapPreview.js";
import { JamlIdeToolbar, type JamlIdeMode } from "./JamlIdeToolbar.js";
import { JamlIdeVisual, type JamlVisualFilter } from "./JamlIdeVisual.js";
import { JimboColorOption } from "../ui/tokens.js";
import { jamlTextToVisualFilter, visualFilterToJamlText } from "../utils/jamlVisualFilter.js";

export interface JamlIdeSearchResult {
  seed: string;
  score?: number;
  tallyColumns?: number[];
  tallyLabels?: string[];
}

export interface JamlIdeProps {
  /** Controlled value. When provided alongside `onChange`, the editor is fully controlled. */
  jaml?: string;
  /** Initial value for uncontrolled mode. Ignored when `jaml` is provided on first render. */
  defaultJaml?: string;
  /** Subscriber for every edit. Optional — the editor still works without it. */
  onChange?: (jaml: string) => void;
  defaultMode?: JamlIdeMode;
  searchResults?: JamlIdeSearchResult[];
  className?: string;
  title?: string;
  actions?: React.ReactNode;
  codePlaceholder?: string;
  onSearch?: () => void;
  isSearching?: boolean;
  /**
   * Controlled visual filter. When provided alongside `onVisualFilterChange`, the Visual tab
   * is fully controlled by the parent. When absent, the Visual tab auto-derives from the text.
   */
  visualFilter?: JamlVisualFilter;
  onVisualFilterChange?: (filter: JamlVisualFilter) => void;
}

export type { JamlVisualFilter } from "./JamlIdeVisual.js";
export type { JamlVisualClause, JamlZone } from "./JamlIdeVisual.js";

function TallyBar({ value, max }: { value: number; max: number }) {
  const pct = max > 0 ? Math.min(1, value / max) : 0;
  return (
    <div style={{ flex: 1, height: 4, borderRadius: 999, background: `${JimboColorOption.DARK_GREY}88`, overflow: "hidden" }}>
      <div
        style={{
          height: "100%",
          width: `${pct * 100}%`,
          borderRadius: 999,
          background: value > 0 ? JimboColorOption.GREEN : JimboColorOption.GREY,
          transition: "width 200ms ease",
        }}
      />
    </div>
  );
}

function ResultsView({ results }: { results: JamlIdeSearchResult[] }) {
  const [expanded, setExpanded] = useState<string | null>(null);

  if (results.length === 0) {
    return (
      <div
        style={{
          border: `1px dashed ${JimboColorOption.DARK_GREY}`,
          borderRadius: 10,
          padding: 16,
          fontSize: 12,
          color: JimboColorOption.GREY,
          background: `${JimboColorOption.DARKEST}88`,
          textAlign: "center",
        }}
      >
        No results yet. Run a search to find seeds.
      </div>
    );
  }

  const labels = results[0]?.tallyLabels ?? [];
  const maxScore = Math.max(...results.map((r) => r.score ?? 0));

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      {results.map((result) => {
        const isOpen = expanded === result.seed;
        const hasTally = result.tallyColumns && result.tallyColumns.length > 0 && labels.length > 0;

        return (
          <div
            key={result.seed}
            style={{
              borderRadius: 10,
              border: `1px solid ${isOpen ? JimboColorOption.GOLD + "55" : JimboColorOption.PANEL_EDGE}`,
              background: isOpen ? `${JimboColorOption.GOLD}0a` : `${JimboColorOption.DARKEST}cc`,
              overflow: "hidden",
              transition: "border-color 120ms",
            }}
          >
            <button
              type="button"
              onClick={() => hasTally && setExpanded(isOpen ? null : result.seed)}
              style={{
                width: "100%",
                display: "flex",
                alignItems: "center",
                gap: 10,
                padding: "9px 12px",
                background: "none",
                border: "none",
                cursor: hasTally ? "pointer" : "default",
                color: "inherit",
                textAlign: "left",
              }}
            >
              <span
                style={{
                  fontFamily: "m6x11plus, monospace",
                  fontWeight: 700,
                  fontSize: 14,
                  letterSpacing: 1,
                  color: JimboColorOption.GOLD_TEXT,
                  minWidth: 80,
                }}
              >
                {result.seed}
              </span>

              {result.score !== undefined ? (
                <>
                  <TallyBar value={result.score} max={maxScore} />
                  <span
                    style={{
                      fontSize: 12,
                      fontWeight: 700,
                      color: result.score > 0 ? JimboColorOption.GREEN_TEXT : JimboColorOption.GREY,
                      minWidth: 36,
                      textAlign: "right",
                    }}
                  >
                    {result.score}
                  </span>
                </>
              ) : null}

              {hasTally ? (
                <span style={{ fontSize: 10, color: JimboColorOption.GREY, marginLeft: 2 }}>
                  {isOpen ? "▲" : "▼"}
                </span>
              ) : null}
            </button>

            {isOpen && hasTally ? (
              <div
                style={{
                  borderTop: `1px solid ${JimboColorOption.PANEL_EDGE}`,
                  padding: "8px 12px 10px",
                  display: "flex",
                  flexDirection: "column",
                  gap: 5,
                }}
              >
                {labels.map((label, i) => {
                  const val = result.tallyColumns![i] ?? 0;
                  const maxVal = Math.max(...results.map((r) => r.tallyColumns?.[i] ?? 0));
                  return (
                    <div key={label} style={{ display: "flex", alignItems: "center", gap: 8 }}>
                      <span
                        style={{
                          fontSize: 11,
                          color: val > 0 ? JimboColorOption.WHITE : JimboColorOption.GREY,
                          minWidth: 140,
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                          whiteSpace: "nowrap",
                        }}
                      >
                        {label}
                      </span>
                      <TallyBar value={val} max={maxVal} />
                      <span
                        style={{
                          fontSize: 11,
                          fontWeight: 700,
                          color: val > 0 ? JimboColorOption.GREEN_TEXT : JimboColorOption.DARK_GREY,
                          minWidth: 24,
                          textAlign: "right",
                        }}
                      >
                        {val}
                      </span>
                    </div>
                  );
                })}
              </div>
            ) : null}
          </div>
        );
      })}
    </div>
  );
}

export function JamlIde({
  jaml,
  defaultJaml,
  onChange,
  defaultMode = "code",
  searchResults = [],
  className = "",
  title = "JAML IDE",
  actions,
  codePlaceholder = "Enter JAML...",
  onSearch,
  isSearching = false,
  visualFilter,
  onVisualFilterChange,
}: JamlIdeProps) {
  const [mode, setMode] = useState<JamlIdeMode>(defaultMode);
  const [internalText, setInternalText] = useState<string>(jaml ?? defaultJaml ?? "");
  const [lastJamlProp, setLastJamlProp] = useState<string | undefined>(jaml);

  // Adjust-state-during-render: sync controlled `jaml` prop into internal text.
  if (jaml !== lastJamlProp) {
    setLastJamlProp(jaml);
    if (jaml !== undefined) setInternalText(jaml);
  }

  const text = internalText;

  const handleTextChange = (next: string) => {
    setInternalText(next);
    onChange?.(next);
  };

  // Derived visual filter state (used only when not externally controlled).
  // Cache the last successfully parsed filter so a mid-edit invalid state
  // doesn't flash the visual panel empty.
  const [lastParsedText, setLastParsedText] = useState<string>("");
  const [lastParsedFilter, setLastParsedFilter] = useState<JamlVisualFilter>(() =>
    jamlTextToVisualFilter(jaml ?? defaultJaml ?? ""),
  );

  // Adjust-state-during-render: reparse when text changes (only if not controlled).
  if (visualFilter === undefined && text !== lastParsedText) {
    try {
      const parsed = jamlTextToVisualFilter(text);
      setLastParsedText(text);
      setLastParsedFilter(parsed);
    } catch {
      // Keep previous filter on parse error — don't flash empty.
      setLastParsedText(text);
    }
  }

  const activeFilter: JamlVisualFilter = visualFilter ?? lastParsedFilter;

  const handleVisualFilterChange = (next: JamlVisualFilter) => {
    if (onVisualFilterChange) {
      // Controlled: let parent own both.
      onVisualFilterChange(next);
    } else {
      // Uncontrolled: round-trip through text so textarea stays source of truth.
      const nextText = visualFilterToJamlText(next);
      setInternalText(nextText);
      setLastParsedFilter(next);
      setLastParsedText(nextText);
      onChange?.(nextText);
    }
  };

  const results = useMemo(() => searchResults, [searchResults]);

  return (
    <div
      className={className}
      style={{
        display: "flex",
        flexDirection: "column",
        minHeight: 420,
        borderRadius: 12,
        overflow: "hidden",
        border: `2px solid ${JimboColorOption.BORDER_SILVER}`,
        boxShadow: `0 3px 0 0 ${JimboColorOption.BORDER_SOUTH}`,
        background: JimboColorOption.DARK_GREY,
        color: JimboColorOption.WHITE,
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 12,
          padding: "10px 14px",
          borderBottom: `1px solid ${JimboColorOption.PANEL_EDGE}`,
          background: JimboColorOption.TEAL_GREY,
        }}
      >
        <div>
          <div style={{ fontSize: 14, fontWeight: 800, fontFamily: "m6x11plus, monospace", color: JimboColorOption.GOLD_TEXT }}>{title}</div>
          <div style={{ fontSize: 11, color: JimboColorOption.GREY }}>Jimbo's Ante Markup Language</div>
        </div>
        {actions ? <div style={{ display: "flex", alignItems: "center", gap: 8 }}>{actions}</div> : null}
      </div>

      <JamlIdeToolbar mode={mode} onModeChange={setMode} resultCount={results.length} onSearch={onSearch} isSearching={isSearching} />

      <div style={{ flex: 1, minHeight: 0, overflow: "auto", background: JimboColorOption.DARKEST }}>
        {mode === "visual" ? (
          <JamlIdeVisual filter={activeFilter} onChange={handleVisualFilterChange} />
        ) : null}

        {mode === "code" ? (
          <textarea
            title="JAML IDE Editor"
            value={text}
            onChange={(event) => handleTextChange(event.target.value)}
            placeholder={codePlaceholder}
            spellCheck={false}
            autoCapitalize="off"
            autoCorrect="off"
            style={{
              width: "100%",
              minHeight: 320,
              resize: "vertical",
              border: 0,
              outline: 0,
              padding: 16,
              background: "transparent",
              color: JimboColorOption.WHITE,
              fontFamily: "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace",
              fontSize: 13,
              lineHeight: 1.7,
              boxSizing: "border-box",
            }}
          />
        ) : null}

        {mode === "map" ? <JamlMapPreview jaml={text} /> : null}

        {mode === "results" ? (
          <div style={{ padding: 12 }}>
            <ResultsView results={results} />
          </div>
        ) : null}
      </div>
    </div>
  );
}
