import type { Completion, CompletionContext, CompletionResult } from "@codemirror/autocomplete";
import { getCompletions, type CompletionItem } from "jaml-lang";

// jaml-lang's getCompletions is context-aware (root key vs. discriminator
// vs. clause key vs. enum value) and reads straight from Motely.Schema.cs's
// generated vocabulary — the same source the VS Code extension and LSP
// server use. This file only adapts its text+offset contract to CodeMirror's
// CompletionContext/CompletionResult shapes; it holds no vocabulary of its
// own.

function toCmCompletion(item: CompletionItem): Completion {
  return {
    label: item.label,
    type: item.kind === "keyword" || item.kind === "field" ? "property" : "constant",
    detail: item.detail,
    info: item.documentation,
    apply: item.insertText,
  };
}

export function jamlCompletions(context: CompletionContext): CompletionResult | null {
  const word = context.matchBefore(/[\w-:]*/);
  if (!word || (word.from === word.to && !context.explicit)) {
    return null;
  }

  // jaml-lang's getContext recomputes the prefix itself from text-before-cursor,
  // so it needs the real cursor offset, not word.from.
  const items = getCompletions(context.state.doc.toString(), context.pos);
  if (items.length === 0) return null;

  return { from: word.from, options: items.map(toCmCompletion) };
}
