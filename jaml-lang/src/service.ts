// JAML language service — diagnostics, completions, hover, symbols.
// Consumes the AST from parser.ts and the vocabulary from vocab.ts.

import {
  parseJaml,
  findNodeAtPosition,
  findKeyParent,
  findSectionKey,
  getLineText,
  getWordAtPosition,
  getLinePrefix,
  type JamlNode,
  type Position,
} from "./parser.js";
import {
  isValidRootKey,
  isValidClauseKey,
  isValidSectionKey,
  getValueSetForClause,
  ROOT_KEY_DOCS,
  KEY_DOCS,
  ROOT_KEYS,
  CLAUSE_KEYS,
  PROPERTY_KEYS,
  SOURCE_KEYS,
  DECKS,
  STAKES,
  EDITIONS,
  ENHANCEMENTS,
  SEALS,
  STICKERS,
  RANKS,
  SUITS,
  RARITIES,
  ANY_VALUE,
  type ClauseKey,
} from "./vocab.js";

export type Severity = 1 | 2 | 3 | 4; // Error, Warning, Information, Hint
export type CompletionKind = "keyword" | "enum" | "field" | "value";

export interface JamlDiagnostic {
  range: { start: { line: number; character: number }; end: { line: number; character: number } };
  message: string;
  severity: Severity;
  source: string;
  code: string;
}

export interface JamlCompletion {
  label: string;
  kind: CompletionKind;
  detail?: string;
  documentation?: string;
  insertText?: string;
}

export interface JamlHover {
  contents: string;
  range: { start: { line: number; character: number }; end: { line: number; character: number } };
}

export interface DocumentSymbol {
  name: string;
  detail?: string;
  kind: "field" | "array" | "object";
  range: { start: { line: number; character: number }; end: { line: number; character: number } };
  selectionRange: { start: { line: number; character: number }; end: { line: number; character: number } };
  children?: DocumentSymbol[];
}

// ── Utilities ───────────────────────────────────────────────────────────────

function posFromOffset(text: string, offset: number): { line: number; char: number } {
  let line = 0;
  let char = 0;
  for (let i = 0; i < offset && i < text.length; i++) {
    if (text[i] === "\n") {
      line++;
      char = 0;
    } else {
      char++;
    }
  }
  return { line, char };
}

function isAnyValue(val: string): boolean {
  return val === ANY_VALUE || val.toLowerCase() === "any";
}

function toLspRange(range: { start: Position; end: Position }): JamlDiagnostic["range"] {
  return {
    start: { line: range.start.line, character: range.start.char },
    end: { line: range.end.line, character: range.end.char },
  };
}

// ── Diagnostics ─────────────────────────────────────────────────────────────

export function getDiagnostics(text: string): JamlDiagnostic[] {
  const diagnostics: JamlDiagnostic[] = [];
  const doc = parseJaml(text);

  if (doc.children.length === 0) return diagnostics;

  const root = doc.children[0];
  if (root.type !== "mapping") {
    diagnostics.push({
      range: toLspRange(root.range),
      message: "JAML root must be a mapping (key: value pairs).",
      severity: 1,
      source: "jaml",
      code: "root-not-mapping",
    });
    return diagnostics;
  }

  // Validate root keys — only check key nodes (even indices in mapping children)
  for (let i = 0; i < root.children.length; i += 2) {
    const child = root.children[i];
    if (child.type === "scalar" && child.parent === root) {
      const key = child.value;
      if (!isValidRootKey(key)) {
        diagnostics.push({
          range: toLspRange(child.range),
          message: `Unknown root key "${key}". Valid: ${ROOT_KEYS.join(", ")}.`,
          severity: 2,
          source: "jaml",
          code: "unknown-root-key",
        });
      }
    }
  }

  // Validate sections and their children — only check key nodes
  for (let i = 0; i < root.children.length; i += 2) {
    const child = root.children[i];
    if (child.type === "scalar" && isValidSectionKey(child.value)) {
      // Find the value node (next element in the pair)
      const valNode = root.children[i + 1];
      if (!valNode || valNode.type !== "sequence") {
        diagnostics.push({
          range: toLspRange(child.range),
          message: `Section "${child.value}" must contain a sequence (list with - items).`,
          severity: 1,
          source: "jaml",
          code: "section-not-sequence",
        });
        continue;
      }

      // Validate each clause in the sequence
      for (const clause of valNode.children) {
        if (clause.type === "mapping") {
          validateClause(clause, diagnostics);
        } else if (clause.type === "scalar" && clause.value) {
          // Scalar in a section — probably invalid unless it's an or/and with nested
          diagnostics.push({
            range: toLspRange(clause.range),
            message: "Clause must be a mapping (key: value).",
            severity: 1,
            source: "jaml",
            code: "clause-not-mapping",
          });
        }
      }
    }
  }

  return diagnostics;
}

function validateClause(clause: JamlNode, diagnostics: JamlDiagnostic[]) {
  const seenKeys = new Set<string>();
  let hasItemType = false;

  for (let i = 0; i < clause.children.length; i += 2) {
    const keyNode = clause.children[i];
    const valNode = clause.children[i + 1];
    if (!keyNode || keyNode.type !== "scalar") continue;
    const key = keyNode.value;

    if (seenKeys.has(key)) {
      diagnostics.push({
        range: toLspRange(keyNode.range),
        message: `Duplicate key "${key}" in clause.`,
        severity: 2,
        source: "jaml",
        code: "duplicate-clause-key",
      });
    }
    seenKeys.add(key);

    if (!isValidClauseKey(key)) {
      diagnostics.push({
        range: toLspRange(keyNode.range),
        message: `Unknown clause key "${key}".`,
        severity: 2,
        source: "jaml",
        code: "unknown-clause-key",
      });
      continue;
    }

    // Check if it's an item-type key (joker, tarotCard, etc.)
    const valueSet = getValueSetForClause(key);
    if (valueSet && valNode) {
      hasItemType = true;
      if (valueSet.length > 0) {
        if (valNode.type === "scalar" && !isAnyValue(valNode.value)) {
          if (!valueSet.includes(valNode.value)) {
            diagnostics.push({
              range: toLspRange(valNode.range),
              message: `Invalid value "${valNode.value}" for "${key}". Valid options include: ${valueSet.slice(0, 8).join(", ")}${valueSet.length > 8 ? "…" : ""}.`,
              severity: 1,
              source: "jaml",
              code: "invalid-enum-value",
            });
          }
        } else if (valNode && valNode.type === "sequence") {
          // Array value for plural keys like jokers: [Baron, Mime]
          for (const item of valNode.children) {
            if (item.type === "scalar" && !isAnyValue(item.value) && !valueSet.includes(item.value)) {
              diagnostics.push({
                range: toLspRange(item.range),
                message: `Invalid value "${item.value}" in "${key}" array.`,
                severity: 1,
                source: "jaml",
                code: "invalid-array-enum-value",
              });
            }
          }
        }
      }
    }

    // Validate property values
    if (key === "deck" && valNode && valNode.type === "scalar") {
      if (!isAnyValue(valNode.value) && !DECKS.includes(valNode.value as any)) {
        diagnostics.push({
          range: toLspRange(valNode.range),
          message: `Invalid deck "${valNode.value}". Valid: ${DECKS.join(", ")}.`,
          severity: 1,
          source: "jaml",
          code: "invalid-deck",
        });
      }
    }
    if (key === "stake" && valNode && valNode.type === "scalar") {
      if (!isAnyValue(valNode.value) && !STAKES.includes(valNode.value as any)) {
        diagnostics.push({
          range: toLspRange(valNode.range),
          message: `Invalid stake "${valNode.value}". Valid: ${STAKES.join(", ")}.`,
          severity: 1,
          source: "jaml",
          code: "invalid-stake",
        });
      }
    }
    if (key === "edition" && valNode && valNode.type === "scalar") {
      if (!isAnyValue(valNode.value) && !EDITIONS.includes(valNode.value as any)) {
        diagnostics.push({
          range: toLspRange(valNode.range),
          message: `Invalid edition "${valNode.value}". Valid: ${EDITIONS.join(", ")}, Any.`,
          severity: 1,
          source: "jaml",
          code: "invalid-edition",
        });
      }
    }
    if (key === "enhancement" && valNode && valNode.type === "scalar") {
      if (!isAnyValue(valNode.value) && !ENHANCEMENTS.includes(valNode.value as any)) {
        diagnostics.push({
          range: toLspRange(valNode.range),
          message: `Invalid enhancement "${valNode.value}". Valid: ${ENHANCEMENTS.join(", ")}, Any.`,
          severity: 1,
          source: "jaml",
          code: "invalid-enhancement",
        });
      }
    }
    if (key === "seal" && valNode && valNode.type === "scalar") {
      if (!isAnyValue(valNode.value) && !SEALS.includes(valNode.value as any)) {
        diagnostics.push({
          range: toLspRange(valNode.range),
          message: `Invalid seal "${valNode.value}". Valid: ${SEALS.join(", ")}, Any.`,
          severity: 1,
          source: "jaml",
          code: "invalid-seal",
        });
      }
    }
    if ((key === "sticker" || key === "stickers") && valNode && valNode.type === "scalar") {
      if (!isAnyValue(valNode.value) && !STICKERS.includes(valNode.value as any)) {
        diagnostics.push({
          range: toLspRange(valNode.range),
          message: `Invalid sticker "${valNode.value}". Valid: ${STICKERS.join(", ")}, Any.`,
          severity: 1,
          source: "jaml",
          code: "invalid-sticker",
        });
      }
    }
    if (key === "rarity" && valNode && valNode.type === "scalar") {
      if (!isAnyValue(valNode.value) && !RARITIES.includes(valNode.value as any)) {
        diagnostics.push({
          range: toLspRange(valNode.range),
          message: `Invalid rarity "${valNode.value}". Valid: ${RARITIES.join(", ")}, Any.`,
          severity: 1,
          source: "jaml",
          code: "invalid-rarity",
        });
      }
    }
    if (key === "rank" && valNode && valNode.type === "scalar") {
      if (!isAnyValue(valNode.value) && !RANKS.includes(valNode.value as any)) {
        diagnostics.push({
          range: toLspRange(valNode.range),
          message: `Invalid rank "${valNode.value}". Valid: ${RANKS.join(", ")}, Any.`,
          severity: 1,
          source: "jaml",
          code: "invalid-rank",
        });
      }
    }
    if (key === "suit" && valNode && valNode.type === "scalar") {
      if (!isAnyValue(valNode.value) && !SUITS.includes(valNode.value as any)) {
        diagnostics.push({
          range: toLspRange(valNode.range),
          message: `Invalid suit "${valNode.value}". Valid: ${SUITS.join(", ")}, Any.`,
          severity: 1,
          source: "jaml",
          code: "invalid-suit",
        });
      }
    }
    if (key === "sources" && valNode && valNode.type === "mapping") {
      for (let j = 0; j < valNode.children.length; j += 2) {
        const srcKey = valNode.children[j];
        if (srcKey.type === "scalar" && !SOURCE_KEYS.includes(srcKey.value as any)) {
          diagnostics.push({
            range: toLspRange(srcKey.range),
            message: `Unknown source key "${srcKey.value}". Valid: ${SOURCE_KEYS.join(", ")}.`,
            severity: 2,
            source: "jaml",
            code: "unknown-source-key",
          });
        }
      }
    }
  }

  if (!hasItemType && clause.children.length > 0) {
    const firstKey = clause.children[0];
    if (firstKey.type === "scalar" && firstKey.value !== "or" && firstKey.value !== "and" && firstKey.value !== "score" && firstKey.value !== "label" && firstKey.value !== "min" && firstKey.value !== "max") {
      diagnostics.push({
        range: toLspRange(clause.range),
        message: `Clause should have an item-type key (joker, tarotCard, etc.) or be an or/and group.`,
        severity: 2,
        source: "jaml",
        code: "missing-item-type",
      });
    }
  }
}

// ── Completions ─────────────────────────────────────────────────────────────

export function getCompletions(text: string, offset: number): JamlCompletion[] {
  const pos = posFromOffset(text, offset);
  const lineText = getLineText(text, pos.line);
  const prefix = getLinePrefix(text, pos.line, pos.char);
  const doc = parseJaml(text);
  const node = findNodeAtPosition(doc, pos.line, pos.char);

  const results: JamlCompletion[] = [];

  // Determine context from indentation and prefix
  const indent = getIndent(lineText);
  const trimmedPrefix = prefix.trim();

  // If we're at the very top level (indent 0), complete root keys
  if (indent === 0 && !trimmedPrefix.includes(":") && !trimmedPrefix.startsWith("-")) {
    for (const key of ROOT_KEYS) {
      results.push({
        label: key,
        kind: "keyword",
        detail: "Root key",
        documentation: ROOT_KEY_DOCS[key] ?? `JAML root key: ${key}`,
        insertText: key + ": ",
      });
    }
    return results;
  }

  // If we're inside a section (must/should/mustNot) and at dash level
  if (trimmedPrefix.startsWith("-")) {
    // After dash, complete clause keys
    for (const key of CLAUSE_KEYS) {
      results.push({
        label: key,
        kind: "field",
        detail: "Clause key",
        documentation: KEY_DOCS[key] ?? `JAML clause key: ${key}`,
        insertText: key + ": ",
      });
    }
    return results;
  }

  // If we're after a key with colon, complete values
  const colonMatch = prefix.match(/^(\s*)(\w[\w-]*)\s*:\s*/);
  if (colonMatch) {
    const keyName = colonMatch[2];
    const valueSet = getValueSetForClause(keyName);
    if (valueSet) {
      results.push({ label: ANY_VALUE, kind: "enum", detail: "Wildcard", documentation: "Matches any value of this type." });
      for (const val of valueSet) {
        results.push({ label: val, kind: "enum", detail: keyName });
      }
      return results;
    }
    if (keyName === "deck") {
      for (const val of DECKS) results.push({ label: val, kind: "enum", detail: "Deck" });
      return results;
    }
    if (keyName === "stake") {
      for (const val of STAKES) results.push({ label: val, kind: "enum", detail: "Stake" });
      return results;
    }
    if (keyName === "edition") {
      results.push({ label: ANY_VALUE, kind: "enum" });
      for (const val of EDITIONS) results.push({ label: val, kind: "enum", detail: "Edition" });
      return results;
    }
    if (keyName === "enhancement") {
      results.push({ label: ANY_VALUE, kind: "enum" });
      for (const val of ENHANCEMENTS) results.push({ label: val, kind: "enum", detail: "Enhancement" });
      return results;
    }
    if (keyName === "seal") {
      results.push({ label: ANY_VALUE, kind: "enum" });
      for (const val of SEALS) results.push({ label: val, kind: "enum", detail: "Seal" });
      return results;
    }
    if (keyName === "sticker" || keyName === "stickers") {
      results.push({ label: ANY_VALUE, kind: "enum" });
      for (const val of STICKERS) results.push({ label: val, kind: "enum", detail: "Sticker" });
      return results;
    }
    if (keyName === "rarity") {
      results.push({ label: ANY_VALUE, kind: "enum" });
      for (const val of RARITIES) results.push({ label: val, kind: "enum", detail: "Rarity" });
      return results;
    }
    if (keyName === "rank" || keyName === "erraticRank") {
      results.push({ label: ANY_VALUE, kind: "enum" });
      for (const val of RANKS) results.push({ label: val, kind: "enum", detail: "Rank" });
      return results;
    }
    if (keyName === "suit" || keyName === "erraticSuit") {
      results.push({ label: ANY_VALUE, kind: "enum" });
      for (const val of SUITS) results.push({ label: val, kind: "enum", detail: "Suit" });
      return results;
    }
    if (keyName === "source" || keyName === "sources") {
      for (const val of SOURCE_KEYS) results.push({ label: val, kind: "keyword", detail: "Source" });
      return results;
    }
  }

  // If we're inside a clause mapping (after first line), complete property keys
  const keyParent = node ? findKeyParent(node) : null;
  if (keyParent) {
    for (const key of PROPERTY_KEYS) {
      results.push({
        label: key,
        kind: "field",
        detail: "Property",
        documentation: KEY_DOCS[key] ?? `Property: ${key}`,
        insertText: key + ": ",
      });
    }
    return results;
  }

  // Default: if inside a section but not in a clause, offer clause keys
  const sectionKey = node ? findSectionKey(node) : null;
  if (sectionKey) {
    for (const key of CLAUSE_KEYS) {
      results.push({
        label: key,
        kind: "field",
        detail: "Clause key",
        documentation: KEY_DOCS[key] ?? `Clause key: ${key}`,
        insertText: key + ": ",
      });
    }
    return results;
  }

  return results;
}

function getIndent(line: string): number {
  let i = 0;
  while (i < line.length && (line[i] === " " || line[i] === "\t")) i++;
  return i;
}

// ── Hover ───────────────────────────────────────────────────────────────────

export function getHover(text: string, offset: number): JamlHover | null {
  const pos = posFromOffset(text, offset);
  const word = getWordAtPosition(text, pos.line, pos.char);
  if (!word) return null;

  const doc = parseJaml(text);
  const node = findNodeAtPosition(doc, pos.line, pos.char);

  // Check if we're on a key
  if (node && node.type === "scalar" && node.parent?.type === "mapping") {
    const docText = ROOT_KEY_DOCS[word] ?? KEY_DOCS[word];
    if (docText) {
      return {
        contents: `**${word}**\n\n${docText}`,
        range: toLspRange(node.range),
      };
    }
  }

  // Check if we're on a value that has a known value set
  if (node && node.type === "scalar" && node.keyName) {
    const valueSet = getValueSetForClause(node.keyName);
    if (valueSet && valueSet.includes(word)) {
      return {
        contents: `**${word}** — ${node.keyName} value`,
        range: toLspRange(node.range),
      };
    }
  }

  return null;
}

// ── Document Symbols ────────────────────────────────────────────────────────

export function getDocumentSymbols(text: string): DocumentSymbol[] {
  const doc = parseJaml(text);
  const symbols: DocumentSymbol[] = [];

  if (doc.children.length === 0) return symbols;
  const root = doc.children[0];
  if (root.type !== "mapping") return symbols;

  // Find name for root symbol
  let rootName = "JAML Filter";
  for (let i = 0; i < root.children.length; i += 2) {
    const key = root.children[i];
    if (key.type === "scalar" && key.value === "name" && root.children[i + 1]) {
      rootName = root.children[i + 1].value || rootName;
      break;
    }
  }

  symbols.push({
    name: rootName,
    kind: "object",
    range: toLspRange(root.range),
    selectionRange: toLspRange(root.range),
    children: [],
  });

  for (let i = 0; i < root.children.length; i += 2) {
    const key = root.children[i];
    const val = root.children[i + 1];
    if (!key || key.type !== "scalar") continue;

    if (isValidSectionKey(key.value) && val && val.type === "sequence") {
      const sectionSym: DocumentSymbol = {
        name: key.value,
        detail: `${val.children.length} clause${val.children.length !== 1 ? "s" : ""}`,
        kind: "array",
        range: toLspRange(key.range),
        selectionRange: toLspRange(key.range),
        children: [],
      };

      for (const clause of val.children) {
        if (clause.type === "mapping" && clause.children.length > 0) {
          const firstKey = clause.children[0];
          if (firstKey.type === "scalar") {
            const clauseName = firstKey.value;
            let clauseDetail = "";
            if (clause.children[1] && clause.children[1].type === "scalar") {
              clauseDetail = clause.children[1].value;
            }
            sectionSym.children!.push({
              name: clauseName,
              detail: clauseDetail,
              kind: "field",
              range: toLspRange(clause.range),
              selectionRange: toLspRange(firstKey.range),
            });
          }
        }
      }

      symbols.push(sectionSym);
    } else if (key.value === "deck" || key.value === "stake" || key.value === "author") {
      symbols.push({
        name: key.value,
        detail: val?.value,
        kind: "field",
        range: toLspRange(key.range),
        selectionRange: toLspRange(key.range),
      });
    }
  }

  return symbols;
}
