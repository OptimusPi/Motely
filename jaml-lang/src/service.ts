// The JAML language service — editor-agnostic.
//
// Pure functions over (text, offset). No LSP, no CodeMirror, no DOM. The LSP
// server adapts these to vscode-languageserver types; a CodeMirror adapter (or
// the MCP app) can call the exact same functions so every surface agrees.
//
//   getDiagnostics(text)        -> YAML syntax errors + Zod structural issues
//   getCompletions(text,offset) -> context-aware keys + enum values
//   getHover(text,offset)       -> docs for the key/enum under the cursor
//   getDocumentSymbols(text)    -> must/should/mustNot outline
//
// The fast structural gate (this file) is layer 1. The authoritative semantic
// layer is Motely WASM `parseJaml` — callers that have it (the LSP server in
// node, a worker on the web) merge those diagnostics in; see mergeDiagnostics.

import {
  parseDocument,
  isMap,
  isSeq,
  isScalar,
  type Node,
  type Pair,
  type Document,
} from "yaml";
import { JamlConfigSchema } from "./authoring.js";
import * as Vocab from "./vocab.generated.js";

// ---------------------------------------------------------------------------
// Editor-neutral types (LSP-shaped: 0-based line/character).
// ---------------------------------------------------------------------------

export interface Position {
  line: number;
  character: number;
}
export interface Range {
  start: Position;
  end: Position;
}

export enum Severity {
  Error = 1,
  Warning = 2,
  Information = 3,
  Hint = 4,
}

export interface Diagnostic {
  range: Range;
  message: string;
  severity: Severity;
  source: string;
  code?: string;
}

export type CompletionKind = "keyword" | "enum" | "field" | "value";

export interface CompletionItem {
  label: string;
  kind: CompletionKind;
  detail?: string;
  documentation?: string;
  /** Text to insert; defaults to label. Keys insert `label: `. */
  insertText?: string;
}

export interface Hover {
  contents: string;
  range?: Range;
}

export interface DocumentSymbol {
  name: string;
  detail?: string;
  kind: "field" | "array" | "object";
  range: Range;
  selectionRange: Range;
  children?: DocumentSymbol[];
}

// ---------------------------------------------------------------------------
// Vocabulary: which JAML key accepts which enum, mirrors authoring.ts.
// ---------------------------------------------------------------------------

const ENUM_FOR_KEY: Record<string, readonly string[]> = {
  joker: Vocab.MotelyJoker,
  jokers: Vocab.MotelyJoker,
  commonJoker: Vocab.MotelyJokerCommon,
  commonJokers: Vocab.MotelyJokerCommon,
  uncommonJoker: Vocab.MotelyJokerUncommon,
  uncommonJokers: Vocab.MotelyJokerUncommon,
  rareJoker: Vocab.MotelyJokerRare,
  rareJokers: Vocab.MotelyJokerRare,
  legendaryJoker: Vocab.MotelyJokerLegendary,
  legendaryJokers: Vocab.MotelyJokerLegendary,
  voucher: Vocab.MotelyVoucher,
  vouchers: Vocab.MotelyVoucher,
  tarotCard: Vocab.MotelyTarotCard,
  tarotCards: Vocab.MotelyTarotCard,
  spectralCard: Vocab.MotelySpectralCard,
  spectralCards: Vocab.MotelySpectralCard,
  planetCard: Vocab.MotelyPlanetCard,
  boss: Vocab.MotelyBossBlind,
  tag: Vocab.MotelyTag,
  tags: Vocab.MotelyTag,
  smallBlindTag: Vocab.MotelyTag,
  smallBlindTags: Vocab.MotelyTag,
  bigBlindTag: Vocab.MotelyTag,
  bigBlindTags: Vocab.MotelyTag,
  event: Vocab.MotelyEventType,
  edition: Vocab.MotelyItemEdition,
  seal: Vocab.MotelyItemSeal,
  enhancement: Vocab.MotelyItemEnhancement,
  rank: Vocab.MotelyStandardcardRank,
  erraticRank: Vocab.MotelyStandardcardRank,
  suit: Vocab.MotelyStandardcardSuit,
  erraticSuit: Vocab.MotelyStandardcardSuit,
  stickers: Vocab.MotelyJokerSticker,
  deck: Vocab.MotelyDeck,
  stake: Vocab.MotelyStake,
};

/** Keys where the literal `Any` is also valid (EnumOrAny<T> in C#). */
const ANY_OK = new Set([
  "joker",
  "commonJoker",
  "uncommonJoker",
  "rareJoker",
  "legendaryJoker",
]);

/** Keys whose value is a list of clauses (the clause-list owners). */
const CLAUSE_LIST_KEYS = new Set([
  "must",
  "should",
  "mustNot",
  "and",
  "or",
  "clauses",
]);

const ROOT_KEYS = [
  "id",
  "name",
  "author",
  "dateCreated",
  "description",
  "deck",
  "stake",
  "defaults",
  "must",
  "should",
  "mustNot",
  "seeds",
  "hashtags",
];

const DEFAULTS_KEYS = ["antes", "boosterPacks", "shopItems", "score"];

const SOURCES_KEYS = [
  "shopItems",
  "boosterPacks",
  "minShopItem",
  "maxShopItem",
  "earlyAntesMaxPack",
  "tags",
  "requireMega",
  "charmTag",
  "etherealTag",
  "judgement",
  "wraith",
  "rareTag",
  "uncommonTag",
  "soulCard",
  "arcanaPacks",
  "spectralPacks",
  "riffRaff",
  "purpleSealOrEightBall",
  "emperor",
  "sixthSense",
  "seance",
  "certificate",
  "incantation",
  "familiar",
  "grim",
  "deckDraw",
  "uncommonShopJokers",
  "rareShopJokers",
  "commonShopJokers",
  "allShopJokers",
];

// Selector + common + compound keys legal on a clause (mirrors authoring.ts).
const CLAUSE_KEYS = [
  // selectors
  "joker",
  "jokers",
  "commonJoker",
  "commonJokers",
  "uncommonJoker",
  "uncommonJokers",
  "rareJoker",
  "rareJokers",
  "legendaryJoker",
  "legendaryJokers",
  "voucher",
  "vouchers",
  "tarotCard",
  "tarotCards",
  "spectralCard",
  "spectralCards",
  "planetCard",
  "boss",
  "tag",
  "tags",
  "smallBlindTag",
  "smallBlindTags",
  "bigBlindTag",
  "bigBlindTags",
  "standardCard",
  "standardCards",
  "erraticRank",
  "erraticSuit",
  "erraticCard",
  "startingDraw",
  "event",
  // numeric event "roll budget" clauses
  "luckyMoney",
  "luckyMult",
  "misprintMult",
  "wheelOfFortune",
  "cavendishExtinct",
  "grosMichelExtinct",
  "spaceLevelup",
  "businessPayout",
  "bloodstoneTrigger",
  "parkingPayout",
  "glassDestroy",
  "wheelStaysFlipped",
  // common props
  "antes",
  "score",
  "min",
  "max",
  "label",
  "edition",
  "stickers",
  "seal",
  "enhancement",
  "rank",
  "suit",
  "rolls",
  "soulEditionRolls",
  "soulCardOnly",
  "sources",
  // compound logic
  "and",
  "or",
  "clauses",
  "mode",
];

// One-line docs for hover + completion detail. Not exhaustive — the high-signal
// keys a filter author reaches for.
const KEY_DOC: Record<string, string> = {
  id: "Stable identifier for this filter.",
  name: "Human-readable filter name.",
  author: "Filter author.",
  description: "What this filter looks for.",
  deck: "Starting deck the search runs on.",
  stake: "Difficulty stake the search runs on.",
  defaults: "Default `antes` / `shopItems` / `boosterPacks` / `score` applied to every clause that doesn't set its own.",
  must: "**Hard requirements.** Every clause here must match or the seed is rejected.",
  should: "**Scored wants.** Each matching clause adds its `score`; non-matches don't reject.",
  mustNot: "**Exclusions.** If any clause here matches, the seed is rejected.",
  seeds: "Explicit seed list to check (seed-list search mode).",
  hashtags: "Free-form tags for organizing filters.",
  joker: "Match a joker by name. `Any` matches any joker.",
  jokers: "Match any joker in this list (OR).",
  voucher: "Match a voucher by name.",
  tarotCard: "Match a tarot card by name.",
  spectralCard: "Match a spectral card by name.",
  planetCard: "Match a planet card by name.",
  boss: "Match a boss blind by name.",
  tag: "Match a skip tag by name.",
  standardCard: "Match a playing card (rank/suit/seal/enhancement/edition).",
  antes: "Antes to search, 1–8. e.g. `[1, 2]`.",
  score: "Points added when a `should` clause matches.",
  min: "Minimum count of matches required.",
  max: "Maximum count of matches allowed.",
  edition: "Foil / Holographic / Polychrome / Negative / None.",
  seal: "Gold / Red / Blue / Purple / None.",
  enhancement: "Bonus / Mult / Wild / Glass / Steel / Stone / Gold / Lucky / None.",
  stickers: "Eternal / Perishable / Rental.",
  rank: "Playing-card rank (Two … Ace).",
  suit: "Playing-card suit (Clubs / Diamonds / Hearts / Spades).",
  erraticRank: "Required rank in an Erratic-deck draw.",
  erraticSuit: "Required suit in an Erratic-deck draw.",
  sources: "Restrict where the item may come from (shop slots, packs, specific tarot streams, …).",
  and: "All nested clauses must match.",
  or: "At least one nested clause must match.",
  clauses: "Nested clause group.",
  event: "Match a probabilistic game event.",
};

// ---------------------------------------------------------------------------
// Offset <-> Position helpers.
// ---------------------------------------------------------------------------

function lineStarts(text: string): number[] {
  const starts = [0];
  for (let i = 0; i < text.length; i++) {
    if (text.charCodeAt(i) === 10 /* \n */) starts.push(i + 1);
  }
  return starts;
}

function posAt(offset: number, starts: number[]): Position {
  // binary search for greatest start <= offset
  let lo = 0;
  let hi = starts.length - 1;
  while (lo < hi) {
    const mid = (lo + hi + 1) >> 1;
    if (starts[mid] <= offset) lo = mid;
    else hi = mid - 1;
  }
  return { line: lo, character: offset - starts[lo] };
}

function rangeOfNode(node: Node, starts: number[]): Range | undefined {
  const r = (node as { range?: [number, number, number] }).range;
  if (!r) return undefined;
  return { start: posAt(r[0], starts), end: posAt(r[1], starts) };
}

function wholeRange(text: string, starts: number[]): Range {
  return { start: { line: 0, character: 0 }, end: posAt(text.length, starts) };
}

// ---------------------------------------------------------------------------
// Diagnostics.
// ---------------------------------------------------------------------------

export function getDiagnostics(text: string): Diagnostic[] {
  if (text.trim() === "") return [];
  const starts = lineStarts(text);
  const diags: Diagnostic[] = [];

  let doc: Document.Parsed;
  try {
    doc = parseDocument(text, { prettyErrors: false, keepSourceTokens: true });
  } catch (e) {
    diags.push({
      range: wholeRange(text, starts),
      message: e instanceof Error ? e.message : String(e),
      severity: Severity.Error,
      source: "jaml",
    });
    return diags;
  }

  for (const err of doc.errors) {
    const [s, e] = err.pos ?? [0, 1];
    diags.push({
      range: { start: posAt(s, starts), end: posAt(Math.max(e, s + 1), starts) },
      message: err.message,
      severity: Severity.Error,
      source: "jaml",
      code: err.code,
    });
  }
  // Broken YAML — don't pile structural errors on top of a parse failure.
  if (doc.errors.length) return diags;

  for (const warn of doc.warnings) {
    const [s, e] = warn.pos ?? [0, 1];
    diags.push({
      range: { start: posAt(s, starts), end: posAt(Math.max(e, s + 1), starts) },
      message: warn.message,
      severity: Severity.Warning,
      source: "jaml",
      code: warn.code,
    });
  }

  let value: unknown;
  try {
    value = doc.toJS({ maxAliasCount: -1 });
  } catch {
    return diags;
  }
  if (value == null || typeof value !== "object") return diags;

  const result = JamlConfigSchema.safeParse(value);
  if (result.success) return diags;

  const hasName =
    typeof (value as Record<string, unknown>).name === "string" &&
    (value as Record<string, unknown>).name !== "";

  for (const issue of result.error.issues) {
    // strict() root rejects unknown keys — underline each offending key, not the root.
    if (issue.code === "unrecognized_keys") {
      const container = nodeAtPath(doc, issue.path);
      for (const key of issue.keys) {
        const keyNode = findKeyNode(container, key);
        diags.push({
          range:
            (keyNode && rangeOfNode(keyNode, starts)) ??
            wholeRange(text, starts),
          message: `Unknown key \`${key}\`. Not part of the JAML filter schema.`,
          severity: Severity.Error,
          source: "jaml",
          code: issue.code,
        });
      }
      continue;
    }

    // Missing `id` is non-fatal: the engine derives one from name/filename.
    if (
      issue.path.length === 1 &&
      issue.path[0] === "id" &&
      issue.code === "invalid_type"
    ) {
      diags.push({
        range: topKeyRange(doc, starts) ?? { start: { line: 0, character: 0 }, end: { line: 0, character: 1 } },
        message: hasName
          ? "No `id:` — the engine will derive one from `name`. Set `id:` to be explicit."
          : "No `id:` or `name:` — add at least one to identify this filter.",
        severity: Severity.Warning,
        source: "jaml",
        code: "missing-id",
      });
      continue;
    }

    const node = nodeAtPath(doc, issue.path);
    const range = (node && rangeOfNode(node, starts)) ?? wholeRange(text, starts);
    diags.push({
      range,
      message: issue.message + pathSuffix(issue.path),
      severity: Severity.Error,
      source: "jaml",
      code: issue.code,
    });
  }

  return diags;
}

function pathSuffix(path: ReadonlyArray<string | number>): string {
  return path.length ? `  (at ${path.join("/")})` : "";
}

function nodeAtPath(
  doc: Document.Parsed,
  path: ReadonlyArray<string | number>,
): Node | null {
  if (path.length === 0) return (doc.contents as Node) ?? null;
  const n = doc.getIn(path as (string | number)[], true);
  return n && typeof n === "object" && "range" in (n as object)
    ? (n as Node)
    : null;
}

function findKeyNode(container: Node | null, key: string): Node | null {
  if (!container || !isMap(container)) return null;
  for (const item of container.items as Pair[]) {
    if (isScalar(item.key) && item.key.value === key) return item.key as Node;
  }
  return null;
}

/** Range of the document's first key (used to anchor doc-level warnings). */
function topKeyRange(doc: Document.Parsed, starts: number[]): Range | undefined {
  const c = doc.contents;
  if (c && isMap(c) && c.items.length) {
    const k = (c.items[0] as Pair).key;
    if (isScalar(k)) return rangeOfNode(k as Node, starts);
  }
  return undefined;
}

// ---------------------------------------------------------------------------
// Completions.
// ---------------------------------------------------------------------------

export function getCompletions(text: string, offset: number): CompletionItem[] {
  const starts = lineStarts(text);
  const pos = posAt(offset, starts);
  const before = text.slice(starts[pos.line], offset);

  // value after `key:` on the same line  (incl. flow arrays `key: [a, b`)
  const valMatch = before.match(
    /^(\s*)(?:-\s*)?([A-Za-z][A-Za-z0-9]*)\s*:\s*(.*)$/,
  );
  if (valMatch) {
    const key = valMatch[2];
    const after = valMatch[3];
    const arr = ENUM_FOR_KEY[key];
    if (!arr) return []; // numeric / free-form value
    const partial = lastToken(after);
    return enumCompletions(arr, key, partial);
  }

  // block-sequence item:  `  - WeeJok`
  const dashMatch = before.match(/^(\s*)-\s*([A-Za-z0-9]*)$/);
  if (dashMatch) {
    const indent = dashMatch[1].length;
    const partial = dashMatch[2];
    const owner = nearestShallowerKey(text, starts, pos.line, indent);
    if (owner && ENUM_FOR_KEY[owner.key]) {
      return enumCompletions(ENUM_FOR_KEY[owner.key], owner.key, partial);
    }
    if (owner && CLAUSE_LIST_KEYS.has(owner.key)) {
      return keyCompletions(CLAUSE_KEYS, partial);
    }
    return [];
  }

  // key position: a bare word (optionally after `- `), nothing else typed yet
  const keyMatch = before.match(/^(\s*)(?:-\s*)?([A-Za-z][A-Za-z0-9]*)?$/);
  if (keyMatch) {
    const indent = keyMatch[1].length;
    const partial = keyMatch[2] ?? "";
    const container = findContainer(text, starts, pos.line, indent);
    return keyCompletions(keysForContainer(container), partial);
  }

  return [];
}

function lastToken(s: string): string {
  // strip everything up to the last `[` or `,` so flow arrays complete per-item
  const m = s.match(/[[,]\s*([^,\]]*)$/);
  return (m ? m[1] : s).trim();
}

function enumCompletions(
  arr: readonly string[],
  key: string,
  partial: string,
): CompletionItem[] {
  const values = ANY_OK.has(key) ? ["Any", ...arr] : arr;
  const lower = partial.toLowerCase();
  return values
    .filter((v) => v.toLowerCase().includes(lower))
    .slice(0, 200)
    .map((v) => ({
      label: v,
      kind: "enum" as const,
      detail: key,
    }));
}

function keyCompletions(keys: string[], partial: string): CompletionItem[] {
  const lower = partial.toLowerCase();
  return keys
    .filter((k) => k.toLowerCase().includes(lower))
    .map((k) => ({
      label: k,
      kind: "field" as const,
      insertText: `${k}: `,
      documentation: KEY_DOC[k],
    }));
}

function keysForContainer(container: Container): string[] {
  switch (container) {
    case "clause":
      return CLAUSE_KEYS;
    case "sources":
      return SOURCES_KEYS;
    case "defaults":
      return DEFAULTS_KEYS;
    case "root":
      return ROOT_KEYS;
  }
}

type Container = "root" | "clause" | "sources" | "defaults";

/** Nearest previous non-blank line whose indent is < `indent` and is `key:`. */
function nearestShallowerKey(
  text: string,
  starts: number[],
  line: number,
  indent: number,
): { key: string; indent: number } | null {
  for (let l = line - 1; l >= 0; l--) {
    const lt = lineText(text, starts, l);
    if (lt.trim() === "" || lt.trimStart().startsWith("#")) continue;
    const ind = indentOf(lt);
    if (ind >= indent) continue;
    const m = lt.match(/^\s*(?:-\s*)?([A-Za-z][A-Za-z0-9]*)\s*:/);
    if (m) return { key: m[1], indent: ind };
    // a shallower line that isn't a key (e.g. a bare `-`) still lowers the bar
    indent = ind;
  }
  return null;
}

/** Climb shallower keys until one tells us the container kind. */
function findContainer(
  text: string,
  starts: number[],
  line: number,
  indent: number,
): Container {
  let cur = indent;
  for (let guard = 0; guard < 200; guard++) {
    const owner = nearestShallowerKey(text, starts, line, cur);
    if (!owner) return "root";
    if (owner.key === "sources") return "sources";
    if (owner.key === "defaults") return "defaults";
    if (CLAUSE_LIST_KEYS.has(owner.key)) return "clause";
    // a plain property key shallower than us — its own parent decides
    cur = owner.indent;
    if (cur === 0) return "root";
  }
  return "root";
}

function lineText(text: string, starts: number[], line: number): string {
  const from = starts[line];
  const to = line + 1 < starts.length ? starts[line + 1] : text.length;
  return text.slice(from, to).replace(/\r?\n$/, "");
}

function indentOf(line: string): number {
  const m = line.match(/^(\s*)/);
  return m ? m[1].length : 0;
}

// ---------------------------------------------------------------------------
// Hover.
// ---------------------------------------------------------------------------

export function getHover(text: string, offset: number): Hover | null {
  const starts = lineStarts(text);
  const pos = posAt(offset, starts);
  const line = lineText(text, starts, pos.line);
  const col = pos.character;

  // token under cursor
  let s = col;
  let e = col;
  while (s > 0 && /[A-Za-z0-9_]/.test(line[s - 1])) s--;
  while (e < line.length && /[A-Za-z0-9_]/.test(line[e])) e++;
  if (s === e) return null;
  const token = line.slice(s, e);
  const range: Range = {
    start: { line: pos.line, character: s },
    end: { line: pos.line, character: e },
  };

  // key?  `<token>:`  or  `- <token>:`
  const keyMatch = line.match(/^\s*(?:-\s*)?([A-Za-z][A-Za-z0-9]*)\s*:/);
  if (keyMatch && keyMatch[1] === token && KEY_DOC[token]) {
    return { contents: `**${token}** — ${KEY_DOC[token]}`, range };
  }

  // enum value?  find the owning key on this line and check membership
  const valMatch = line.match(/^\s*(?:-\s*)?([A-Za-z][A-Za-z0-9]*)\s*:\s*(.*)$/);
  if (valMatch) {
    const arr = ENUM_FOR_KEY[valMatch[1]];
    if (arr && arr.includes(token)) {
      return {
        contents: `\`${token}\` — value for **${valMatch[1]}**`,
        range,
      };
    }
  }
  // enum value as a block-seq item: `  - WeeJoker`
  if (/^\s*-\s*[A-Za-z0-9]+\s*$/.test(line)) {
    const owner = nearestShallowerKey(text, starts, pos.line, indentOf(line));
    if (owner && ENUM_FOR_KEY[owner.key]?.includes(token)) {
      return { contents: `\`${token}\` — value for **${owner.key}**`, range };
    }
  }

  return null;
}

// ---------------------------------------------------------------------------
// Document symbols (outline).
// ---------------------------------------------------------------------------

export function getDocumentSymbols(text: string): DocumentSymbol[] {
  if (text.trim() === "") return [];
  const starts = lineStarts(text);
  let doc: Document.Parsed;
  try {
    doc = parseDocument(text);
  } catch {
    return [];
  }
  const root = doc.contents;
  if (!root || !isMap(root)) return [];

  const symbols: DocumentSymbol[] = [];
  for (const pair of root.items as Pair[]) {
    if (!isScalar(pair.key)) continue;
    const key = String(pair.key.value);
    const keyRange = rangeOfNode(pair.key as Node, starts);
    if (!keyRange) continue;
    const valueRange =
      (pair.value && rangeOfNode(pair.value as Node, starts)) ?? keyRange;
    const full: Range = { start: keyRange.start, end: valueRange.end };

    if (CLAUSE_LIST_KEYS.has(key) && pair.value && isSeq(pair.value)) {
      const children: DocumentSymbol[] = [];
      for (const item of pair.value.items as Node[]) {
        const r = rangeOfNode(item, starts);
        if (!r) continue;
        children.push({
          name: clauseLabel(item),
          kind: "object",
          range: r,
          selectionRange: r,
        });
      }
      symbols.push({
        name: key,
        detail: `${children.length}`,
        kind: "array",
        range: full,
        selectionRange: keyRange,
        children,
      });
    } else {
      symbols.push({
        name: key,
        kind: "field",
        range: full,
        selectionRange: keyRange,
      });
    }
  }
  return symbols;
}

/** A short label for a clause: its first recognized selector, e.g. `joker: WeeJoker`. */
function clauseLabel(item: Node): string {
  if (!isMap(item)) return "clause";
  for (const pair of item.items as Pair[]) {
    if (!isScalar(pair.key)) continue;
    const k = String(pair.key.value);
    if (k === "and") return "and";
    if (k === "or") return "or";
    if (ENUM_FOR_KEY[k] || k === "standardCard" || k === "erraticCard") {
      const v =
        pair.value && isScalar(pair.value) ? String(pair.value.value) : "…";
      return `${k}: ${v}`;
    }
  }
  // fall back to the first key
  const first = (item.items as Pair[])[0];
  return first && isScalar(first.key) ? String(first.key.value) : "clause";
}

// ---------------------------------------------------------------------------
// Diagnostic merge — layer the authoritative WASM result on top of the fast gate.
// ---------------------------------------------------------------------------

/**
 * Merge structural (fast) diagnostics with authoritative ones from the engine,
 * dropping duplicates that cover the same range+message. The semantic layer is
 * produced by whoever can call `Motely.parseJaml` (LSP server / web worker).
 */
export function mergeDiagnostics(
  fast: Diagnostic[],
  authoritative: Diagnostic[],
): Diagnostic[] {
  const seen = new Set(
    fast.map((d) => `${d.range.start.line}:${d.range.start.character}:${d.message}`),
  );
  const out = [...fast];
  for (const d of authoritative) {
    const key = `${d.range.start.line}:${d.range.start.character}:${d.message}`;
    if (!seen.has(key)) {
      out.push(d);
      seen.add(key);
    }
  }
  return out;
}
