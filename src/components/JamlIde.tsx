"use client";

import React, { useMemo, useState } from "react";
import { JimboBalatroFooter } from "../ui/footer.js";
import { JamlMapPreview } from "./JamlMapPreview.js";
import {
  JamlMapEditor,
  CategoryMenu,
  JokerPicker,
  CategoryPicker,
  VOUCHER_PICKER_CONFIG,
  TAG_PICKER_CONFIG,
  BOSS_PICKER_CONFIG,
  TAROT_PICKER_CONFIG,
  PLANET_PICKER_CONFIG,
  SPECTRAL_PICKER_CONFIG,
  PACK_PICKER_CONFIG,
  type SlotCategory,
  type SlotSelection,
} from "./jamlMap/index.js";
import { JamlIdeToolbar, type JamlIdeMode } from "./JamlIdeToolbar.js";
import { JamlIdeVisual, type JamlVisualFilter, type JamlZone, type JamlVisualClause } from "./JamlIdeVisual.js";
import { JamlCodeEditor } from "./JamlCodeEditor.js";
import { JimboColorOption } from "../ui/tokens.js";
import { JimboModal } from "../ui/panel.js";
import { jamlTextToVisualFilter, visualFilterToJamlText } from "../utils/jamlVisualFilter.js";
import { DeckSprite } from "./DeckSprite.js";
import { DECK_OPTIONS, STAKE_OPTIONS } from "../lib/data/constants.js";

const CATEGORY_CONFIG_MAP = {
  voucher: VOUCHER_PICKER_CONFIG,
  tag: TAG_PICKER_CONFIG,
  boss: BOSS_PICKER_CONFIG,
  tarot: TAROT_PICKER_CONFIG,
  planet: PLANET_PICKER_CONFIG,
  spectral: SPECTRAL_PICKER_CONFIG,
  pack: PACK_PICKER_CONFIG,
} as const;

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
  style?: React.CSSProperties;
  title?: string;
  subtitle?: React.ReactNode;
  compactHeader?: boolean;
  actions?: React.ReactNode;
  codePlaceholder?: string;
  onSearch?: () => void;
  isSearching?: boolean;
  /** Hide the Balatro attribution footer. Default: false (always shown). */
  hideFooter?: boolean;
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

function ResultsView({ results, jaml }: { results: JamlIdeSearchResult[]; jaml: string }) {
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

  const maxScore = Math.max(...results.map((r) => r.score ?? 0));

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      {results.map((result) => {
        const isOpen = expanded === result.seed;
        const hasTally = result.tallyColumns && result.tallyColumns.length > 0;

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
                  fontWeight: "normal",
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
                <span style={{ fontSize: 11, color: JimboColorOption.GREY, marginLeft: 2 }}>
                  {isOpen ? "▲" : "▼"}
                </span>
              ) : null}
            </button>

            {isOpen && hasTally ? (
              <div
                style={{
                  borderTop: `1px solid ${JimboColorOption.PANEL_EDGE}`,
                  padding: "4px",
                  display: "flex",
                  flexDirection: "column",
                  gap: 8,
                }}
              >
                <JamlMapPreview
                  jaml={jaml}
                  tallyColumns={result.tallyColumns}
                  tallyLabels={result.tallyLabels}
                />

                {/* Fallback/Detailed tally list for debugging or non-visual clauses */}
                <div style={{ padding: "4px 8px 8px", display: "flex", flexDirection: "column", gap: 5 }}>
                  <span style={{ fontSize: 9, color: JimboColorOption.WHITE, opacity: 0.8 }}>Raw Tally Data</span>
                  {(result.tallyLabels ?? []).map((label, i) => {
                    const val = result.tallyColumns![i] ?? 0;
                    if (val === 0) return null;
                    return (
                      <div key={label} style={{ display: "flex", alignItems: "center", gap: 8 }}>
                        <span style={{ fontSize: 10, color: JimboColorOption.WHITE, flex: 1 }}>{label}</span>
                        <span style={{ fontSize: 10, color: JimboColorOption.GREEN_TEXT }}>{val}</span>
                      </div>
                    );
                  })}
                </div>
              </div>
            ) : null}
          </div>
        );
      })}
    </div>
  );
}

function readRootValue(jaml: string, key: "deck" | "stake", fallback: string): string {
  const match = jaml.match(new RegExp(`^${key}:\\s*(.+)$`, "m"));
  return match?.[1]?.trim() || fallback;
}

function setRootValue(jaml: string, key: "deck" | "stake", value: string): string {
  const line = `${key}: ${value}`;
  const pattern = new RegExp(`^${key}:\\s*.*$`, "m");
  if (pattern.test(jaml)) {
    return jaml.replace(pattern, line);
  }
  const trimmed = jaml.trimEnd();
  return trimmed.length > 0 ? `${line}\n${trimmed}` : line;
}

function DeckStakeSelector({
  jaml,
  onChange,
}: {
  jaml: string;
  onChange: (next: string) => void;
}) {
  const deck = readRootValue(jaml, "deck", "Red");
  const stake = readRootValue(jaml, "stake", "White");

  const setDeck = (nextDeck: string) => onChange(setRootValue(jaml, "deck", nextDeck));
  const setStake = (nextStake: string) => onChange(setRootValue(jaml, "stake", nextStake));

  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8, minWidth: 0 }}>
      <DeckSprite deck={deck} stake={stake} size={compactDeckSpriteSize} />
      <select
        value={deck}
        onChange={(event) => setDeck(event.currentTarget.value)}
        style={selectorStyle}
      >
        {DECK_OPTIONS.map((option) => (
          <option key={option} value={option}>{option}</option>
        ))}
      </select>
      <select
        value={stake}
        onChange={(event) => setStake(event.currentTarget.value)}
        style={selectorStyle}
      >
        {STAKE_OPTIONS.map((option) => (
          <option key={option} value={option}>{option}</option>
        ))}
      </select>
    </div>
  );
}

const compactDeckSpriteSize = 24;

const selectorStyle: React.CSSProperties = {
  minWidth: 0,
  height: 28,
  borderRadius: 8,
  border: `1px solid ${JimboColorOption.PANEL_EDGE}`,
  background: JimboColorOption.DARKEST,
  color: JimboColorOption.WHITE,
  fontFamily: "m6x11plus, monospace",
  fontSize: 12,
  padding: "0 8px",
};

export function JamlIde({
  jaml,
  defaultJaml,
  onChange,
  defaultMode = "code",
  searchResults = [],
  className = "",
  style,
  title = "JAML IDE",
  subtitle = "Jimbo's Ante Markup Language",
  compactHeader = false,
  actions,
  codePlaceholder = "Enter JAML...",
  onSearch,
  isSearching = false,
  hideFooter = false,
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

  // Adjust-state-during-render: reparse when text changes (only if not
  // controlled). Gated on `mode === "visual"` so we don't burn CPU parsing
  // on every streamed token while the user is in the .jaml/map/results tab.
  if (visualFilter === undefined && mode === "visual" && text !== lastParsedText) {
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

  // ── Add-clause picker state ──────────────────────────────────────────────
  const [addZone, setAddZone] = useState<JamlZone | null>(null);
  const [pickerFlow, setPickerFlow] = useState<"category" | SlotCategory>("category");

  const handleAddClause = (zone: JamlZone) => {
    setAddZone(zone);
    setPickerFlow("category");
  };

  const handlePickerSelect = (sel: SlotSelection) => {
    if (!addZone) return;
    const clause: JamlVisualClause = {
      id: `${Date.now()}-${Math.random()}`,
      type: sel.clauseKey,
      value: sel.value,
      label: sel.value,
    };
    handleVisualFilterChange({ ...activeFilter, [addZone]: [...activeFilter[addZone], clause] });
    setAddZone(null);
  };

  const handlePickerClose = () => {
    if (pickerFlow !== "category") {
      setPickerFlow("category");
    } else {
      setAddZone(null);
    }
  };

  return (
    <div
      className={className}
      style={{
        display: "flex",
        flexDirection: "column",
        minHeight: 420,
        borderRadius: 12,
        overflow: "hidden",
        overflowX: "clip",
        overscrollBehaviorX: "none",
        maxWidth: "100%",
        border: `2px solid ${JimboColorOption.BORDER_SILVER}`,
        boxShadow: `0 3px 0 0 ${JimboColorOption.BORDER_SOUTH}`,
        background: JimboColorOption.DARK_GREY,
        color: JimboColorOption.WHITE,
        ...style,
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          flexWrap: "wrap",
          gap: compactHeader ? 8 : 12,
          padding: compactHeader ? "8px 10px" : "10px 14px",
          borderBottom: `1px solid ${JimboColorOption.PANEL_EDGE}`,
          background: JimboColorOption.TEAL_GREY,
        }}
      >
        <div>
          <div style={{ fontSize: 16, fontWeight: "normal", fontFamily: "m6x11plus, monospace", color: JimboColorOption.GOLD_TEXT }}>{title}</div>
          {subtitle ? <div style={{ fontSize: 11, color: JimboColorOption.GREY }}>{subtitle}</div> : null}
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap", justifyContent: "flex-end" }}>
          <DeckStakeSelector jaml={text} onChange={handleTextChange} />
          {actions}
        </div>
      </div>

      <JamlIdeToolbar mode={mode} onModeChange={setMode} resultCount={results.length} onSearch={onSearch} isSearching={isSearching} />

      <div style={{ flex: 1, minHeight: 0, overflow: mode === "map" ? "hidden" : "auto", background: JimboColorOption.DARKEST }}>
        {mode === "visual" ? (
          <JamlIdeVisual filter={activeFilter} onChange={handleVisualFilterChange} onAddClause={handleAddClause} />
        ) : null}

        {mode === "code" ? (
          <JamlCodeEditor
            value={text}
            onChange={handleTextChange}
            placeholder={codePlaceholder}
          />
        ) : null}

        {mode === "map" ? <JamlMapEditor onChange={handleTextChange} /> : null}

        {mode === "results" ? (
          <div style={{ padding: 12 }}>
            <ResultsView results={results} jaml={text} />
          </div>
        ) : null}
      </div>

      {!hideFooter && <JimboBalatroFooter />}

      <JimboModal open={addZone !== null} onClose={handlePickerClose}>
        {addZone !== null && (
          pickerFlow === "category" ? (
            <CategoryMenu onSelect={(cat) => setPickerFlow(cat)} />
          ) : pickerFlow === "joker" ? (
            <JokerPicker onSelect={handlePickerSelect} onCancel={handlePickerClose} />
          ) : (
            <CategoryPicker
              config={CATEGORY_CONFIG_MAP[pickerFlow as keyof typeof CATEGORY_CONFIG_MAP]}
              onSelect={handlePickerSelect}
              onCancel={handlePickerClose}
            />
          )
        )}
      </JimboModal>
    </div>
  );
}
