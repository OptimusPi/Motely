// CodeMirror 6 ⇆ jaml-lang bridge.
//
// jaml-lang (published npm pkg) is the single source of truth for JAML
// completions + diagnostics, generated from the Motely C# enums. This file is a
// thin adapter that maps its editor-agnostic, LSP-shaped API onto CodeMirror's
// autocomplete + lint extensions. No JAML knowledge lives here — it all comes
// from jaml-lang.

import type { CompletionContext, CompletionResult, Completion } from "@codemirror/autocomplete";
import { linter, type Diagnostic as CMDiagnostic } from "@codemirror/lint";
import type { Text } from "@codemirror/state";
import {
  getCompletions,
  getDiagnostics,
  Severity,
  type CompletionKind,
} from "jaml-lang";

// jaml-lang completion kind -> CodeMirror completion `type` (drives the icon/color).
function kindToType(kind: CompletionKind): Completion["type"] {
  switch (kind) {
    case "field": return "property";
    case "enum": return "enum";
    case "value": return "constant";
    case "keyword": return "keyword";
    default: return "text";
  }
}

/**
 * CodeMirror completion source backed by jaml-lang's context-aware completions.
 * Tap into `joker: ` and you get the joker list; type `Blue` and it narrows to
 * Blueprint. Keys insert `label: `, values insert the bare value.
 */
export function jamlCompletionSource(context: CompletionContext): CompletionResult | null {
  const word = context.matchBefore(/[\w-]*/);
  // Don't pop up on an empty line unless the user explicitly asked (Ctrl-Space).
  if (!context.explicit && word && word.from === word.to) return null;

  const items = getCompletions(context.state.doc.toString(), context.pos);
  if (items.length === 0) return null;

  const options: Completion[] = items.map((it) => ({
    label: it.label,
    type: kindToType(it.kind),
    detail: it.detail,
    info: it.documentation,
    apply: it.insertText ?? it.label,
  }));

  return {
    from: word ? word.from : context.pos,
    options,
    validFor: /^[\w-]*$/,
  };
}

// LSP 0-based {line, character} -> CodeMirror absolute offset, clamped to bounds.
function posToOffset(doc: Text, line: number, character: number): number {
  if (line < 0) return 0;
  if (line >= doc.lines) return doc.length;
  const l = doc.line(line + 1);
  return Math.min(l.from + Math.max(0, character), l.to);
}

/**
 * CodeMirror lint source backed by jaml-lang diagnostics (YAML syntax + Zod
 * structural validation). The Motely WASM engine stays the final authority;
 * this is the fast structural gate.
 */
export const jamlLinter = linter((view): CMDiagnostic[] => {
  const doc = view.state.doc;
  const text = doc.toString();
  return getDiagnostics(text).map((d): CMDiagnostic => ({
    from: posToOffset(doc, d.range.start.line, d.range.start.character),
    to: posToOffset(doc, d.range.end.line, d.range.end.character),
    severity:
      d.severity === Severity.Error
        ? "error"
        : d.severity === Severity.Warning
          ? "warning"
          : "info",
    message: d.message,
    source: d.source,
  }));
});
