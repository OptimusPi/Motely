/**
 * Lightweight JAML context walker.
 * Determines what the editor should offer at a given cursor position
 * without a full YAML parse — walks indentation levels line by line.
 */

import { Discriminators } from "./generated.js";

export type JamlContextKind =
  | "root-key"          // completing a root-level key (name:, deck:, must:, ...)
  | "root-value"        // completing the value of a root key (deck: <HERE>)
  | "discriminator"     // completing a new clause discriminator key
  | "discriminator-value"   // completing the value after a discriminator (joker: <HERE>)
  | "clause-key"        // completing a non-discriminator clause key (antes:, min:, ...)
  | "clause-value"      // completing the value of a clause key
  | "source-key"        // completing a key inside sources: block
  | "source-value"      // completing a value inside sources: block
  | "unknown";

export interface JamlContext {
  kind: JamlContextKind;
  /** The discriminator already present in the current clause, if any. */
  discriminator: string | null;
  /** The partial word being typed (may be empty). */
  prefix: string;
  /** The key being completed, if the cursor is on a value line. */
  valueKey: string | null;
}

/** Returns the indentation level (number of leading spaces) of a line. */
function indent(line: string): number {
  let i = 0;
  while (i < line.length && (line[i] === " " || line[i] === "\t")) i++;
  return i;
}

/** Strip leading `- ` list markers. */
function stripListMarker(line: string): string {
  return line.replace(/^\s*-\s*/, "");
}

/**
 * Determine the editing context at a given offset.
 */
export function getContext(text: string, offset: number): JamlContext {
  const lines = text.split("\n");
  let pos = 0;
  let cursorLine = 0;
  let cursorCol = 0;
  for (let i = 0; i < lines.length; i++) {
    const len = lines[i].length + 1; // +1 for \n
    if (pos + len > offset) {
      cursorLine = i;
      cursorCol = offset - pos;
      break;
    }
    pos += len;
  }

  const currentRaw = lines[cursorLine] ?? "";
  const currentTrimmed = stripListMarker(currentRaw);
  const cursorIndent = indent(currentRaw);

  // Partial word at cursor
  const beforeCursor = currentRaw.slice(0, cursorCol);
  const prefixMatch = beforeCursor.match(/[\w-]*$/);
  const prefix = prefixMatch ? prefixMatch[0] : "";

  // Are we after a colon? (value position)
  const colonValueMatch = beforeCursor.match(/^\s*-?\s*([\w-]+)\s*:\s*([\w-]*)$/);
  const isValuePosition = colonValueMatch != null;
  const valueKey = colonValueMatch ? colonValueMatch[1].toLowerCase() : null;

  // Root level = indent 0, no list marker, not inside must/should/mustNot block
  if (cursorIndent === 0 && !currentRaw.trimStart().startsWith("-")) {
    if (isValuePosition && valueKey) {
      return { kind: "root-value", discriminator: null, prefix, valueKey };
    }
    return { kind: "root-key", discriminator: null, prefix, valueKey: null };
  }

  // Scan backwards to find context: which section we're in, and which clause
  let inClauseList = false;
  let clauseIndent = -1;
  let discriminatorFound: string | null = null;
  let inSourcesBlock = false;
  let sourcesIndent = -1;

  // Straight from the generated Discriminators list — no hand-copied second
  // source of truth to drift from Motely.Schema.cs's output. Matching is
  // case-insensitive since JAML authors don't always hit canonical casing.
  const clauseDiscriminatorsLower = new Set(Discriminators.map((d) => d.toLowerCase()));
  const isDiscriminator = (key: string) => clauseDiscriminatorsLower.has(key.toLowerCase());

  // Walk up from the line above cursor to gather context
  for (let i = cursorLine; i >= 0; i--) {
    const raw = lines[i];
    const trimmed = raw.trimStart();
    const ind = indent(raw);
    const isListItem = trimmed.startsWith("- ");
    const content = isListItem ? trimmed.slice(2).trimStart() : trimmed;

    // Detect sources: block
    if (/^sources\s*:/.test(content) && !inSourcesBlock) {
      if (cursorIndent > ind) {
        inSourcesBlock = true;
        sourcesIndent = ind;
      }
    }

    // Detect list item that is a clause (indent ≥ 2, starts with -)
    if (isListItem && !inClauseList) {
      clauseIndent = ind;
      inClauseList = true;
      // Check all keys of this clause block for a discriminator
      for (let j = i; j <= cursorLine && j < lines.length; j++) {
        const jRaw = lines[j];
        const jInd = indent(jRaw);
        if (j > i && jInd <= clauseIndent) break; // left the clause block
        const jContent = stripListMarker(jRaw);
        const keyMatch = jContent.match(/^([\w-]+)\s*:/);
        if (keyMatch) {
          const k = keyMatch[1];
          if (isDiscriminator(k)) {
            discriminatorFound = k;
            break;
          }
        }
      }
      break;
    }

    // Hit a root-level key → we're inside a section (must/should/mustNot)
    if (ind === 0 && /^(must|should|mustNot)\s*:/.test(trimmed)) {
      inClauseList = true;
      break;
    }
  }

  if (!inClauseList) {
    // Deeper nesting we don't recognise
    return { kind: "unknown", discriminator: null, prefix, valueKey: null };
  }

  if (inSourcesBlock) {
    if (isValuePosition && valueKey) {
      return { kind: "source-value", discriminator: discriminatorFound, prefix, valueKey };
    }
    return { kind: "source-key", discriminator: discriminatorFound, prefix, valueKey: null };
  }

  // We're inside a clause
  if (isValuePosition && valueKey) {
    if (isDiscriminator(valueKey)) {
      return { kind: "discriminator-value", discriminator: valueKey, prefix, valueKey };
    }
    return { kind: "clause-value", discriminator: discriminatorFound, prefix, valueKey };
  }

  if (!discriminatorFound) {
    // First key of a new clause → offer discriminators
    return { kind: "discriminator", discriminator: null, prefix, valueKey: null };
  }

  return { kind: "clause-key", discriminator: discriminatorFound, prefix, valueKey: null };
}
