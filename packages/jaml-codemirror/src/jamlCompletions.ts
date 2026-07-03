import type { Completion, CompletionContext, CompletionResult } from "@codemirror/autocomplete";
import { allKeys } from "./keys.js";
import { getVocabulary, kindForKey } from "./vocab.js";

const keyOptions: Completion[] = allKeys.map((label) => ({
  label,
  type: "property",
}));

const stickerOptions = ["Eternal", "Perishable", "Rental"].map((label) => ({
  label,
  type: "constant",
}));

const ITEM_KEY_PATTERN = /(?:^|\s)([\w]+)\s*:\s*$/;

export function jamlCompletions(context: CompletionContext): CompletionResult | null {
  const word = context.matchBefore(/[\w-:]*/);
  if (!word || (word.from === word.to && !context.explicit)) {
    return null;
  }

  const line = context.state.doc.lineAt(word.from);
  const before = line.text.slice(0, word.from - line.from);
  const vocab = getVocabulary();

  const keyMatch = before.match(ITEM_KEY_PATTERN);
  if (keyMatch) {
    const key = keyMatch[1];
    if (key === "stickers") {
      return { from: word.from, options: stickerOptions };
    }
    const kind = kindForKey(key);
    if (kind && vocab?.[kind]) {
      return {
        from: word.from,
        options: vocab[kind].map((label) => ({ label, type: "constant" })),
      };
    }
  }

  return { from: word.from, options: keyOptions };
}
