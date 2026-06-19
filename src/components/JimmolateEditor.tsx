"use client";

// TODO(jimbo-primitives): the CodeMirror host + status/test chrome use a few
// inline styles (editor min-height, status colors) that pre-date the
// no-inline-style / no-token-in-jsx-style rules. They read from --j-* tokens,
// not raw hex. Refactor to Jimbo primitives alongside JamlCodeEditor.
/* eslint-disable jaml-design/no-inline-style, jaml-design/no-token-in-jsx-style */

import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { EditorView, keymap, lineNumbers, highlightActiveLine, drawSelection, placeholder as cmPlaceholder } from "@codemirror/view";
import { EditorState } from "@codemirror/state";
import { javascript } from "@codemirror/lang-javascript";
import { defaultKeymap, history, historyKeymap, indentWithTab } from "@codemirror/commands";
import { syntaxHighlighting, HighlightStyle } from "@codemirror/language";
import { tags } from "@lezer/highlight";
import { JimboColorOption } from "../ui/tokens.js";
import { JimboPanel, JimboInnerPanel, JimboButton } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";

/**
 * A Jimmolate predicate: per-seed JavaScript that runs INSIDE the Motely search
 * loop (bound to `Motely.jimmolateProbe` pre-boot — see lib/motely/runtime.ts),
 * layered on top of the JAML must/should/mustNot clauses. Return `true` to keep
 * a seed, `false` to reject it.
 */
export type JimmolatePredicate = (seed: string, deck?: number, stake?: number) => boolean;

const DEFAULT_SOURCE = `// Jimmolate — a per-seed JavaScript filter that runs inside the
// search, on top of your JAML must / should / mustNot clauses.
//
// In scope:  seed (string), deck (number), stake (number)
// Return true to KEEP the seed, false to reject it.

return seed.startsWith("A");
`;

// Balatro-flavored JS syntax highlighting — mirrors JamlCodeEditor's palette so
// the Jimmolate box reads as part of the same IDE.
const balatroHighlight = HighlightStyle.define([
  { tag: tags.comment, color: JimboColorOption.GREY, fontStyle: "italic" },
  { tag: [tags.keyword, tags.controlKeyword, tags.operatorKeyword], color: JimboColorOption.RED },
  { tag: [tags.string, tags.special(tags.string)], color: JimboColorOption.GOLD_TEXT },
  { tag: [tags.number, tags.bool, tags.null], color: JimboColorOption.BLUE },
  { tag: [tags.function(tags.variableName), tags.function(tags.propertyName)], color: JimboColorOption.GREEN_TEXT },
  { tag: tags.propertyName, color: JimboColorOption.GREEN_TEXT },
  { tag: [tags.typeName, tags.className], color: JimboColorOption.ORANGE_TEXT },
  { tag: tags.variableName, color: JimboColorOption.WHITE },
  { tag: [tags.operator, tags.punctuation, tags.bracket], color: JimboColorOption.GREY },
]);

const balatroTheme = EditorView.theme(
  {
    "&": {
      backgroundColor: JimboColorOption.DARKEST,
      color: JimboColorOption.WHITE,
      fontSize: "14px",
      height: "100%",
    },
    ".cm-content": {
      fontFamily: "var(--j-font-code, 'JetBrains Mono', ui-monospace, monospace)",
      lineHeight: "23px",
      padding: "12px 0",
      caretColor: JimboColorOption.GOLD_TEXT,
      minHeight: "100%",
    },
    ".cm-gutters": {
      backgroundColor: JimboColorOption.DARKEST,
      color: JimboColorOption.GREY,
      border: "none",
    },
    ".cm-lineNumbers .cm-gutterElement": {
      minWidth: "2ch",
      padding: "0 6px 0 8px",
      fontSize: "14px",
    },
    ".cm-activeLineGutter": { color: JimboColorOption.GOLD_TEXT, backgroundColor: "transparent" },
    ".cm-activeLine": { backgroundColor: `${JimboColorOption.BLACK}20` },
    ".cm-selectionBackground": { backgroundColor: `${JimboColorOption.WHITE}20 !important` },
    "&.cm-focused .cm-selectionBackground": { backgroundColor: `${JimboColorOption.WHITE}20` },
    ".cm-cursor, .cm-dropCursor": { borderLeftColor: JimboColorOption.GOLD_TEXT },
    ".cm-scroller": { overflow: "auto" },
    ".cm-placeholder": { color: JimboColorOption.GREY, fontStyle: "italic", fontSize: "14px" },
  },
  { dark: true },
);

/** Result of compiling the predicate source. */
export interface JimmolateCompileResult {
  predicate: JimmolatePredicate | null;
  error: string | null;
}

/**
 * Compile a Jimmolate source body into a callable predicate. The body runs with
 * `seed`, `deck`, and `stake` in scope and is expected to `return` a boolean.
 *
 * NOTE: this evaluates user-authored JavaScript via `new Function` BY DESIGN —
 * Jimmolate is "bring your own filter". It runs only on the author's machine
 * against their own seeds, exactly like the C#-side probe it feeds.
 */
export function compileJimmolate(source: string): JimmolateCompileResult {
  try {
    // eslint-disable-next-line no-new-func
    const fn = new Function("seed", "deck", "stake", source) as JimmolatePredicate;
    return { predicate: fn, error: null };
  } catch (err) {
    return { predicate: null, error: err instanceof Error ? err.message : String(err) };
  }
}

type TestOutcome =
  | { kind: "idle" }
  | { kind: "pass" }
  | { kind: "fail" }
  | { kind: "error"; message: string };

export interface JimmolateEditorProps {
  /** Initial predicate source (uncontrolled). Defaults to a worked example. */
  defaultValue?: string;
  /** Fired whenever the source text changes. */
  onChange?: (source: string) => void;
  /** Fired whenever the source compiles (or fails to). */
  onPredicateChange?: (result: JimmolateCompileResult) => void;
  /** Initial enabled state (uncontrolled). Defaults to true. */
  defaultEnabled?: boolean;
  /** Fired when the enable toggle flips. */
  onEnabledChange?: (enabled: boolean) => void;
  /** Seed prefilled into the test row. */
  testSeed?: string;
  /** Min height of the code area in px. */
  minHeight?: number;
  className?: string;
}

/**
 * JimmolateEditor — author, compile, and smoke-test a Jimmolate predicate.
 *
 * Emits the compiled predicate via `onPredicateChange`; wire that into
 * `useSearch().startAesthetic(jaml, aesthetic, predicate)` (or the seedlist /
 * random variants) when enabled. Purely an authoring surface — it does not boot
 * Motely or run a search itself.
 */
export function JimmolateEditor({
  defaultValue = DEFAULT_SOURCE,
  onChange,
  onPredicateChange,
  defaultEnabled = true,
  onEnabledChange,
  testSeed = "ALEPH",
  minHeight = 200,
  className = "",
}: JimmolateEditorProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const viewRef = useRef<EditorView | null>(null);

  const [source, setSource] = useState(defaultValue);
  const [enabled, setEnabled] = useState(defaultEnabled);
  const [seed, setSeed] = useState(testSeed);
  const [outcome, setOutcome] = useState<TestOutcome>({ kind: "idle" });

  // Keep latest callbacks in refs so the CodeMirror effect can stay mount-only.
  const onChangeRef = useRef(onChange);
  const onPredicateChangeRef = useRef(onPredicateChange);
  useEffect(() => { onChangeRef.current = onChange; });
  useEffect(() => { onPredicateChangeRef.current = onPredicateChange; });

  const compiled = useMemo(() => compileJimmolate(source), [source]);

  // Surface compile results to the consumer.
  useEffect(() => { onPredicateChangeRef.current?.(compiled); }, [compiled]);

  // Mount the editor once.
  useEffect(() => {
    if (!containerRef.current) return;

    const view = new EditorView({
      state: EditorState.create({
        doc: defaultValue,
        extensions: [
          history(),
          lineNumbers(),
          highlightActiveLine(),
          drawSelection(),
          javascript({ typescript: false }),
          syntaxHighlighting(balatroHighlight),
          balatroTheme,
          EditorView.lineWrapping,
          keymap.of([indentWithTab, ...defaultKeymap, ...historyKeymap]),
          cmPlaceholder("return /* keep this seed? */ true;"),
          EditorView.updateListener.of((update) => {
            if (update.docChanged) {
              const next = update.state.doc.toString();
              setSource(next);
              onChangeRef.current?.(next);
            }
          }),
        ],
      }),
      parent: containerRef.current,
    });

    viewRef.current = view;
    return () => {
      view.destroy();
      viewRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const toggleEnabled = useCallback(() => {
    setEnabled((prev) => {
      const next = !prev;
      onEnabledChange?.(next);
      return next;
    });
  }, [onEnabledChange]);

  const runTest = useCallback(() => {
    if (!compiled.predicate) {
      setOutcome({ kind: "error", message: compiled.error ?? "Predicate did not compile." });
      return;
    }
    try {
      const kept = compiled.predicate(seed, 0, 0);
      setOutcome({ kind: kept ? "pass" : "fail" });
    } catch (err) {
      setOutcome({ kind: "error", message: err instanceof Error ? err.message : String(err) });
    }
  }, [compiled, seed]);

  const statusColor = compiled.error ? "var(--j-red)" : "var(--j-green-text)";
  const statusText = compiled.error ? `Error: ${compiled.error}` : "Compiles ✓";

  return (
    <JimboPanel className={`j-jimmolate ${className}`.trim()}>
      <div
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: "var(--j-space-md)",
          marginBottom: "var(--j-space-md)",
        }}
      >
        <JimboText size="md" className="j-text--gold">Jimmolate</JimboText>
        <JimboButton
          tone={enabled ? "green" : "grey"}
          size="sm"
          onClick={toggleEnabled}
        >
          {enabled ? "Enabled" : "Disabled"}
        </JimboButton>
      </div>

      <JimboInnerPanel>
        <div ref={containerRef} style={{ width: "100%", minHeight }} />
      </JimboInnerPanel>

      <div style={{ marginTop: "var(--j-space-sm)", minHeight: "1.4em" }}>
        <JimboText size="xs" style={{ color: statusColor }}>{statusText}</JimboText>
      </div>

      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: "var(--j-space-md)",
          marginTop: "var(--j-space-md)",
          flexWrap: "wrap",
        }}
      >
        <JimboText size="xs" className="j-text--grey">Test seed</JimboText>
        <input
          className="j-jimmolate__seed"
          value={seed}
          onChange={(e) => setSeed(e.target.value.toUpperCase())}
          spellCheck={false}
          style={{
            fontFamily: "var(--j-font-code, monospace)",
            fontSize: "14px",
            background: "var(--j-surface-inset)",
            color: "var(--j-white)",
            border: "2px solid var(--j-inner-border)",
            borderRadius: "var(--j-radius-sm)",
            padding: "var(--j-space-xs) var(--j-space-sm)",
            width: "12ch",
          }}
        />
        <JimboButton tone="blue" size="sm" onClick={runTest} disabled={!compiled.predicate}>
          Test
        </JimboButton>
        <JimmolateOutcomeBadge outcome={outcome} />
      </div>
    </JimboPanel>
  );
}

function JimmolateOutcomeBadge({ outcome }: { outcome: TestOutcome }) {
  if (outcome.kind === "idle") return null;
  if (outcome.kind === "pass") return <JimboText size="xs" className="j-text--green">Kept (true)</JimboText>;
  if (outcome.kind === "fail") return <JimboText size="xs" className="j-text--grey">Rejected (false)</JimboText>;
  return <JimboText size="xs" style={{ color: "var(--j-red)" }}>{outcome.message}</JimboText>;
}
