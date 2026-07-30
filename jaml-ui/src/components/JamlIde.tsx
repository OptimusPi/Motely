"use client";

// TODO(jimbo-primitives): pre-dates no-inline-style / no-token-in-jsx-style /
// no-inline-component rules. Refactor to compose from Jimbo* primitives once
// screenshot-driven primitive design lands. Remove the disable as each
// section is rewritten. `git grep TODO(jimbo-primitives)` to see the queue.
/* eslint-disable jaml-design/no-inline-style, jaml-design/no-token-in-jsx-style, jaml-design/no-inline-component */

import React, { useState } from "react";
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
import { Jamlyzer } from "./Jamlyzer.js";
import { normalizeJamlSeed } from "./jamlSeedUtils.js";
import { JimboColorOption } from "../ui/tokens.js";
import { JimboButton, JimboModal } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { mergeSeedsIntoJaml } from "../lib/jaml/jamlSeeds.js";
import { jamlTextToVisualFilter, visualFilterToJamlText } from "../utils/jamlVisualFilter.js";
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
  /**
   * Shows a "Load File" button in the toolbar and loads the selected file into the editor.
   * When `onLoadFile` is provided, that callback is used (for example, a mounted library flow).
   * Otherwise the component falls back to a browser file picker.
   */
  showLoadFileButton?: boolean;
  onLoadFile?: () => Promise<string | null> | string | null;
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

function ResultsView({
  results,
  jaml,
  onVerify,
}: {
  results: JamlIdeSearchResult[];
  jaml: string;
  onVerify?: () => void;
}) {
  const [expanded, setExpanded] = useState<string | null>(null);

  if (results.length === 0) {
    return (
      <div
        style={{
          border: `1px dashed ${JimboColorOption.DARK_GREY}`,
          borderRadius: 10,
          padding: 16,
          fontSize: 13,
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
    <div className="j-ide-results">
      {onVerify ? (
        <div className="j-ide-results__bridge">
          <JimboButton tone="green" size="sm" onClick={onVerify}>
            Verify seeds
          </JimboButton>
          <JimboText size="xs" tone="white" className="j-ide-results__bridge-hint">
            Writes {results.length} hit{results.length === 1 ? "" : "s"} to seeds: and opens Test
          </JimboText>
        </div>
      ) : null}

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
            <div
              role={hasTally ? "button" : undefined}
              tabIndex={hasTally ? 0 : -1}
              onClick={() => hasTally && setExpanded(isOpen ? null : result.seed)}
              onKeyDown={(event) => {
                if (!hasTally) return;
                if (event.key === "Enter" || event.key === " ") {
                  event.preventDefault();
                  setExpanded(isOpen ? null : result.seed);
                }
              }}
              style={{
                width: "100%",
                display: "flex",
                alignItems: "center",
                gap: 10,
                padding: "9px 12px",
                cursor: hasTally ? "pointer" : "default",
                color: "inherit",
                textAlign: "left",
              }}
            >
              <span
                style={{
                  fontFamily: "var(--j-font-code)",
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
            </div>

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
                <div style={{ padding: "6px 0 10px", display: "flex", flexDirection: "column", gap: 6 }}>
                  <span style={{ fontSize: 10, color: JimboColorOption.WHITE, opacity: 0.8 }}>Raw Tally Data</span>
                  {(result.tallyLabels ?? []).map((label, i) => {
                    const val = result.tallyColumns![i] ?? 0;
                    if (val === 0) return null;
                    return (
                      <div key={label} style={{ display: "flex", alignItems: "center", gap: 8 }}>
                        <span style={{ fontSize: 11, color: JimboColorOption.WHITE, flex: 1 }}>{label}</span>
                        <span style={{ fontSize: 11, color: JimboColorOption.GREEN_TEXT }}>{val}</span>
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
    </div>
  );
}

async function pickAndReadJamlFile(): Promise<string | null> {
  if (typeof window === "undefined") return null;

  const pickerWindow = window as Window & {
    showOpenFilePicker?: (options?: {
      multiple?: boolean;
      excludeAcceptAllOption?: boolean;
      types?: Array<{ description?: string; accept: Record<string, string[]> }>;
    }) => Promise<Array<{ getFile(): Promise<File> }>>;
  };

  if (pickerWindow.showOpenFilePicker) {
    const [handle] = await pickerWindow.showOpenFilePicker({
      multiple: false,
      excludeAcceptAllOption: false,
      types: [
        {
          description: "JAML Files",
          accept: {
            "text/plain": [".jaml", ".yaml", ".yml", ".txt"],
          },
        },
      ],
    });
    if (!handle) return null;
    const file = await handle.getFile();
    return await file.text();
  }

  return await new Promise<string | null>((resolve) => {
    const input = document.createElement("input");
    input.type = "file";
    input.accept = ".jaml,.yaml,.yml,.txt,text/plain";
    input.onchange = async () => {
      const file = input.files?.[0];
      if (!file) {
        resolve(null);
        return;
      }
      resolve(await file.text());
    };
    input.click();
  });
}


export function JamlIde({
  jaml,
  defaultJaml,
  onChange,
  defaultMode = "code",
  searchResults = [],
  className = "",
  style,
  title,
  subtitle,
  compactHeader = true,
  actions,
  codePlaceholder = "Enter JAML...",
  onSearch,
  isSearching = false,
  showLoadFileButton = false,
  onLoadFile,
  visualFilter,
  onVisualFilterChange,
}: JamlIdeProps) {
  const [mode, setMode] = useState<JamlIdeMode>(defaultMode);
  const [internalText, setInternalText] = useState<string>(jaml ?? defaultJaml ?? "");
  const [lastJamlProp, setLastJamlProp] = useState<string | undefined>(jaml);
  const [isLoadingFile, setIsLoadingFile] = useState(false);

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

  const handleLoadFile = async () => {
    setIsLoadingFile(true);
    try {
      const loadedText = onLoadFile ? await onLoadFile() : await pickAndReadJamlFile();
      if (loadedText === null) return;
      handleTextChange(loadedText);
      setMode("code");
    } catch {
      // Keep this non-fatal so users can keep editing if they cancel or picker fails.
    } finally {
      setIsLoadingFile(false);
    }
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

  const showResultsTab = Boolean(onSearch || searchResults.length > 0);
  const availableModes: JamlIdeMode[] = [
    "visual",
    "code",
    "map",
    ...(showResultsTab ? (["results"] as JamlIdeMode[]) : []),
    "jamlyzer",
  ];
  const headerVisible = Boolean(title || subtitle || actions);

  if (!availableModes.includes(mode)) {
    setMode("code");
  }

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

  const handleVerifyInJamlyzer = () => {
    const seeds = searchResults
      .map((result) => normalizeJamlSeed(result.seed))
      .filter((seed) => seed.length === 8);
    if (seeds.length === 0) return;
    handleTextChange(mergeSeedsIntoJaml(text, seeds, 1000));
    setMode("jamlyzer");
  };

  return (
    <div
      className={`j-ide ${className}`.trim()}
      style={style}
    >
      {headerVisible ? (
        <div className={`j-ide__header ${compactHeader ? "j-ide__header--compact" : ""}`.trim()}>
          <div className="j-ide__header-copy">
            {title ? <div className="j-ide__title">{title}</div> : null}
            {subtitle ? <div className="j-ide__subtitle">{subtitle}</div> : null}
          </div>
          {actions && (
            <div className="j-ide__actions">
              {actions}
            </div>
          )}
        </div>
      ) : null}

      <JamlIdeToolbar
        mode={mode}
        onModeChange={setMode}
        resultCount={searchResults.length}
        showResultsTab={showResultsTab}
        showJamlyzerTab
        onSearch={onSearch}
        isSearching={isSearching}
        onLoadFile={showLoadFileButton ? handleLoadFile : undefined}
        isLoadingFile={isLoadingFile}
      />

      <div className={`j-ide__body ${mode === "map" ? "j-ide__body--map" : ""}`.trim()}>
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
          <div className="j-ide__results">
            <ResultsView results={searchResults} jaml={text} onVerify={handleVerifyInJamlyzer} />
          </div>
        ) : null}

        {mode === "jamlyzer" ? (
          <div className="j-ide__jamlyzer">
            <Jamlyzer jaml={text} />
          </div>
        ) : null}
      </div>

      <JimboModal
        open={addZone !== null}
        onClose={handlePickerClose}
        title={
          pickerFlow === "category"
            ? "Select Category"
            : pickerFlow === "joker"
            ? "Select Joker"
            : CATEGORY_CONFIG_MAP[pickerFlow as keyof typeof CATEGORY_CONFIG_MAP]?.title ?? "Select Item"
        }
        className="j-picker-modal"
      >
        {addZone !== null && (
          pickerFlow === "category" ? (
            <CategoryMenu onSelect={(cat) => setPickerFlow(cat)} />
          ) : pickerFlow === "joker" ? (
            <JokerPicker onSelect={handlePickerSelect} />
          ) : (
            <CategoryPicker
              config={CATEGORY_CONFIG_MAP[pickerFlow as keyof typeof CATEGORY_CONFIG_MAP]}
              onSelect={handlePickerSelect}
            />
          )
        )}
      </JimboModal>
    </div>
  );
}
