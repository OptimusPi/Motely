import {
  Discriminators, RootKeys, Enums,
  DiscriminatorValueEnum, DiscriminatorClauseKeys, DiscriminatorSourceKeys,
  ClauseKeyValueEnum,
} from "./generated.js";

export interface Diagnostic {
  from: number;   // character offset start
  to: number;     // character offset end
  severity: "error" | "warning";
  message: string;
}

// ── LSP-shaped surface (for CodeMirror bridge + VS Code extension) ────────────

export enum Severity { Error = 1, Warning = 2, Information = 3, Hint = 4 }

export interface LspPosition { line: number; character: number }
export interface LspRange { start: LspPosition; end: LspPosition }

export interface LspDiagnostic {
  range: LspRange;
  severity: Severity;
  message: string;
  source: string;
}

/** Returns diagnostics with LSP line/character positions (0-based). */
export function getDiagnostics(text: string): LspDiagnostic[] {
  const raw = validate(text);
  if (raw.length === 0) return [];

  // Build line offset table once
  const offsets: number[] = [0];
  for (let i = 0; i < text.length; i++)
    if (text[i] === "\n") offsets.push(i + 1);

  function posAt(offset: number): LspPosition {
    let lo = 0, hi = offsets.length - 1;
    while (lo < hi) {
      const mid = (lo + hi + 1) >> 1;
      if (offsets[mid] <= offset) lo = mid; else hi = mid - 1;
    }
    return { line: lo, character: offset - offsets[lo] };
  }

  return raw.map((d) => ({
    range: { start: posAt(d.from), end: posAt(d.to) },
    severity: d.severity === "error" ? Severity.Error : Severity.Warning,
    message: d.message,
    source: "jaml-lang",
  }));
}

const ROOT_KEYS = new Set(RootKeys.map((k) => k.toLowerCase()));
const DISC_SET  = new Set(Discriminators.map((k) => k.toLowerCase()));
// Clause-level key -> enum name, case-insensitive. Single source: generated ClauseKeyValueEnum.
const CLAUSE_VALUE_ENUM = new Map(
  Object.entries(ClauseKeyValueEnum).map(([k, v]) => [k.toLowerCase(), v]),
);
// The three root keys that hold a list of clauses. A small explicit set on purpose —
// most of RootKeys (name, deck, stake, seeds, ...) are scalar/metadata, not clause lists.
const SECTION_KEYS = new Set(["must", "should", "mustnot"]);
// min/max/score are shared by every clause type; soulEditionRolls is legendaryJoker-only.
// None of these have an enum, so CLAUSE_VALUE_ENUM never covers them — they'd otherwise get
// no value validation at all (freeform strings).
// min/max/soulEditionRolls are counts — never negative in the real corpus. score is a signed
// tally (real filters use negative score as a penalty, e.g. JamlFilters/impossible.jaml), so
// it only needs to be a valid integer, sign unconstrained.
const NON_NEGATIVE_INT_KEYS = new Set(["min", "max", "souleditionrolls"]);
const INT_KEYS = new Set(["score"]);
const BOOLEAN_KEYS = new Set(["soulcardonly"]);

/** Single case-insensitive definition of the "Any" wildcard, used everywhere it can appear
 *  instead of three separately-written (and, before this, inconsistently-cased) checks. */
function isAnyWildcard(value: string): boolean {
  return value.trim().toLowerCase() === "any";
}

function lineOffsets(text: string): number[] {
  const offsets: number[] = [0];
  for (let i = 0; i < text.length; i++)
    if (text[i] === "\n") offsets.push(i + 1);
  return offsets;
}

function offsetOf(lineOffsets: number[], line: number, col: number): number {
  return (lineOffsets[line] ?? 0) + col;
}

/** Very lightweight YAML key extraction — only cares about the key name. */
function extractKey(raw: string): string | null {
  const m = raw.replace(/^\s*-?\s*/, "").match(/^([\w-]+)\s*:/);
  return m ? m[1] : null;
}

function extractValue(raw: string): string | null {
  const m = raw.match(/:\s*(.+)$/);
  return m ? m[1].trim() : null;
}

function indentOf(raw: string): number {
  let i = 0;
  while (i < raw.length && (raw[i] === " " || raw[i] === "\t")) i++;
  return i;
}

/**
 * Prescan a clause block (list item starting at `start`) and return its canonical
 * discriminator, if any. YAML maps are unordered — the discriminator may appear
 * after other keys — so key validity can only be judged once the whole clause
 * has been seen. There is no flat "all clause keys" fallback on purpose: a key
 * is only ever valid relative to a discriminator.
 */
function findClauseDiscriminator(lines: string[], start: number, clauseIndent: number): string | null {
  for (let j = start; j < lines.length; j++) {
    const raw = lines[j];
    const trimmed = raw.trimStart();
    if (!trimmed || trimmed.startsWith("#")) continue;
    if (j > start && indentOf(raw) <= clauseIndent) break; // next sibling / dedent ends the clause
    const content = j === start ? trimmed.replace(/^-\s*/, "") : trimmed;
    const key = extractKey("  " + content);
    if (key && DISC_SET.has(key.toLowerCase())) {
      return Discriminators.find((d) => d.toLowerCase() === key.toLowerCase()) ?? key;
    }
  }
  return null;
}

/**
 * Validate JAML text. Returns an array of Diagnostics (may be empty).
 * Not a full YAML parser — handles the common subset JAML authors write.
 */
export function validate(text: string): Diagnostic[] {
  const diags: Diagnostic[] = [];
  const lines = text.split("\n");
  const offsets = lineOffsets(text);

  function diag(line: number, col: number, len: number, severity: Diagnostic["severity"], message: string) {
    const from = offsetOf(offsets, line, col);
    diags.push({ from, to: from + len, severity, message });
  }

  // Validate an enum-constrained clause-level key value (scalar like `seal: Gold` or array
  // like `stickers: [Eternal, Rental]`). Flags each unknown member. `Any` is always allowed.
  function checkClauseValueEnum(key: string, rawLine: string, lineIdx: number, ind: number) {
    const enumName = CLAUSE_VALUE_ENUM.get(key.toLowerCase());
    if (!enumName) return;
    const val = extractValue(rawLine);
    if (!val) return;
    const members = Enums[enumName] ?? [];
    const arr = val.match(/^\[(.*)\]$/);
    const tokens = arr
      ? arr[1].split(",").map((t) => t.trim()).filter((t) => t.length > 0)
      : [val];
    for (const token of tokens) {
      const norm = token.replace(/\s+/g, "");
      if (isAnyWildcard(norm)) continue;
      if (members.some((m) => m.toLowerCase() === norm.toLowerCase())) continue;
      const col = rawLine.lastIndexOf(token);
      diag(lineIdx, col >= 0 ? col : ind, token.length, "warning", `Unknown ${enumName} value '${token}'.`);
    }
  }

  // Non-enum scalar validation for keys CLAUSE_VALUE_ENUM has no entry for — min/max/
  // soulEditionRolls (non-negative integers), score (signed integer — real filters use
  // negative scores as a penalty), and soulCardOnly (boolean). Previously these keys had
  // no value validation at all: any string, including nonsense, passed silently.
  function checkScalarValue(key: string, rawLine: string, lineIdx: number, ind: number) {
    const lower = key.toLowerCase();
    const val = extractValue(rawLine);
    if (!val) return;
    const col = rawLine.lastIndexOf(val);
    const at = col >= 0 ? col : ind;
    if (NON_NEGATIVE_INT_KEYS.has(lower)) {
      if (!/^\d+$/.test(val.trim())) {
        diag(lineIdx, at, val.length, "error", `'${key}' must be a non-negative integer, got '${val}'.`);
      }
    } else if (INT_KEYS.has(lower)) {
      if (!/^-?\d+$/.test(val.trim())) {
        diag(lineIdx, at, val.length, "error", `'${key}' must be an integer, got '${val}'.`);
      }
    } else if (BOOLEAN_KEYS.has(lower)) {
      if (!/^(true|false)$/i.test(val.trim())) {
        diag(lineIdx, at, val.length, "error", `'${key}' must be true or false, got '${val}'.`);
      }
    }
  }

  // True for and/or (and any future logic clause) — determined from the real generated
  // ClauseKeys data, not a hardcoded name list.
  function isLogicDiscriminator(disc: string): boolean {
    const keys = DiscriminatorClauseKeys[disc];
    return !!keys && keys.some((k) => k.toLowerCase() === "clauses");
  }

  // A key is valid relative to the clause's discriminator — generated DiscriminatorClauseKeys
  // is the authority for the clause's own keys — OR is itself any other discriminator name.
  // The second half matters: JamlConfigLoader.ValidateClauseKeys unions
  // JamlDiscriminatorRegistry.Entries.Keys into every clause's allowed keys, so ANY
  // discriminator can appear embedded inside ANY other clause as an implicit-AND composed
  // sub-filter (e.g. `smallBlindTag: X` with a sibling `or: [...]` key means "match the tag
  // AND match one of these"). Without this, real corpus filters using that composition read
  // as errors (see JamlFilters/faceding.jaml).
  function checkKeyAllowed(disc: string, key: string, lineIdx: number, raw: string, ind: number) {
    const lower = key.toLowerCase();
    if (DISC_SET.has(lower)) return;
    const allowed = DiscriminatorClauseKeys[disc];
    if (allowed && !allowed.some((k) => k.toLowerCase() === lower)) {
      const col = raw.indexOf(key, ind);
      diag(lineIdx, col, key.length, "error", `Key '${key}' is not valid for ${disc}.`);
    }
  }

  // Discriminator-value check shared by a clause's opening line ("- joker: Blueprint") and a
  // discriminator key appearing later in the same clause body (YAML maps are unordered).
  function checkDiscriminatorValue(key: string, raw: string, lineIdx: number) {
    const val = extractValue(raw);
    if (!val) return;
    const canon = Discriminators.find((d) => d.toLowerCase() === key.toLowerCase()) ?? key;
    const enumName = DiscriminatorValueEnum[canon];
    if (!enumName) return;
    const stripped = val.replace(/\s+/g, "");
    if (stripped.startsWith("[")) return; // skip arrays
    if (isAnyWildcard(val)) return;
    const members = Enums[enumName] ?? [];
    if (members.some((m) => m.toLowerCase() === stripped.toLowerCase())) return;
    const col = raw.lastIndexOf(val);
    diag(lineIdx, col, val.length, "warning", `Unknown ${enumName} value '${val}'.`);
  }

  // Validate a clause's body: every key after its own header line, at indent > headerIndent.
  // `discriminator` may be null (a clause with no recognizable discriminator — its keys were
  // already flagged at the header; body keys still get enum/scalar value checks where they
  // apply). Returns the index of the first line that no longer belongs to this body (its
  // dedent boundary), so the caller can resume scanning from there.
  //
  // Shared by consumeClauseAt (a top-level "- <disc>: ..." list item) and, recursively, by
  // any embedded discriminator key found inside another clause's body — see the DISC_SET
  // branch below and checkKeyAllowed's doc comment for why that composition is legal.
  function consumeClauseBody(discriminator: string | null, bodyStart: number, headerIndent: number): number {
    let inSources = false;
    let sourcesIndent = -1;
    let inWith = false;
    let withIndent = -1;

    let j = bodyStart;
    while (j < lines.length) {
      const raw = lines[j];
      const trimmed = raw.trimStart();
      if (!trimmed || trimmed.startsWith("#")) { j++; continue; }
      const lineInd = indentOf(raw);
      if (lineInd <= headerIndent) break; // dedent: this body has ended

      // and/or's own value can directly BE the nested clause list, with no explicit
      // `clauses:` key at all — JamlConfigLoader.ParseLogic: data.GetClauseList("clauses")
      // ?? data.GetClauseList(discriminator). A bare "- " item here (not already consumed
      // by an explicit `clauses:` block below) is that fallback form:
      //   - and:
      //     - smallBlindTag: NegativeTag
      //     - joker: OopsAll6s
      //     score: 70
      // Gated on the discriminator's real ClauseKeys actually listing "clauses" (i.e. it's
      // a logic clause) rather than hardcoding "and"/"or", so this generalizes to any future
      // logic clause type without a validator change.
      if (trimmed.startsWith("- ") && discriminator && isLogicDiscriminator(discriminator)) {
        j = consumeClauseAt(j, lineInd);
        continue;
      }

      const key = extractKey(raw);
      if (!key) { j++; continue; }

      // entering sources block?
      if (key.toLowerCase() === "sources") {
        if (discriminator) checkKeyAllowed(discriminator, key, j, raw, lineInd);
        inSources = true;
        sourcesIndent = lineInd;
        inWith = false;
        j++;
        continue;
      }

      if (inSources && lineInd > sourcesIndent) {
        if (discriminator) {
          const allowed = DiscriminatorSourceKeys[discriminator];
          if (allowed && !allowed.some((k) => k.toLowerCase() === key.toLowerCase())) {
            const col = raw.indexOf(key, lineInd);
            diag(j, col, key.length, "error", `Unknown source key '${key}' for ${discriminator}.`);
          }
        }
        j++;
        continue;
      }
      if (inSources && lineInd <= sourcesIndent) inSources = false;

      // entering with block? The `with` key itself is gated per-discriminator; with-modifier
      // vocabulary has no codegen table yet, so inside the block we stay silent rather than guess.
      if (key.toLowerCase() === "with") {
        if (discriminator) checkKeyAllowed(discriminator, key, j, raw, lineInd);
        inWith = true;
        withIndent = lineInd;
        j++;
        continue;
      }
      if (inWith && lineInd > withIndent) { j++; continue; }
      if (inWith && lineInd <= withIndent) inWith = false;

      // Nested clause list (and/or's `clauses:` key) — each item is a full clause with its
      // own discriminator, validated by recursing rather than checked as a flat key here.
      if (key.toLowerCase() === "clauses") {
        if (discriminator) checkKeyAllowed(discriminator, key, j, raw, lineInd);
        const clausesIndent = lineInd;
        j++;
        while (j < lines.length) {
          const raw2 = lines[j];
          const trimmed2 = raw2.trimStart();
          if (!trimmed2 || trimmed2.startsWith("#")) { j++; continue; }
          const ind2 = indentOf(raw2);
          if (ind2 <= clausesIndent) break;
          j = trimmed2.startsWith("- ") ? consumeClauseAt(j, ind2) : j + 1;
        }
        continue;
      }

      // Embedded discriminator key (e.g. an `or:` key inside a smallBlindTag clause) —
      // checkKeyAllowed already permits this; validate it the same way a top-level clause
      // is validated: its own scalar/enum value (if any) plus its own body, recursively.
      if (DISC_SET.has(key.toLowerCase())) {
        const canon = Discriminators.find((d) => d.toLowerCase() === key.toLowerCase()) ?? key;
        checkDiscriminatorValue(key, raw, j);
        j = consumeClauseBody(canon, j + 1, lineInd);
        continue;
      }

      // Gate against the discriminator's allowed keys — the only valid question. A clause
      // with no discriminator was already flagged at its list item.
      if (discriminator) checkKeyAllowed(discriminator, key, j, raw, lineInd);

      checkClauseValueEnum(key, raw, j, lineInd);
      checkScalarValue(key, raw, j, lineInd);
      j++;
    }

    return j;
  }

  // Validate one clause list item starting at line `start` (indent `ind`) — its header line
  // plus its full body via consumeClauseBody. Returns the index of the first line that no
  // longer belongs to this clause.
  function consumeClauseAt(start: number, ind: number): number {
    const discriminator = findClauseDiscriminator(lines, start, ind);

    // The list-item line itself: "- <key>: <value>" (key may be the discriminator, or some
    // other clause key if the discriminator appears later — prescan already resolved it).
    const raw = lines[start];
    const content = raw.trimStart().slice(2).trimStart();
    const key = extractKey("  " + content);
    if (key) {
      if (!DISC_SET.has(key.toLowerCase())) {
        if (discriminator) {
          checkKeyAllowed(discriminator, key, start, raw, ind);
        } else {
          const col = raw.indexOf(key, ind);
          diag(start, col, key.length, "error", `Clause has no discriminator (expected a clause type key like joker:, voucher:, tag:, ...).`);
        }
      } else {
        checkDiscriminatorValue(key, raw, start);
      }
    }

    return consumeClauseBody(discriminator, start + 1, ind);
  }

  let inSection = false; // inside must/should/mustNot

  for (let i = 0; i < lines.length; i++) {
    const raw = lines[i];
    const trimmed = raw.trimStart();
    const ind = indentOf(raw);

    // Skip empty / comment lines
    if (!trimmed || trimmed.startsWith("#")) continue;

    // Root level (indent 0, not a list)
    if (ind === 0 && !trimmed.startsWith("-")) {
      inSection = false;
      const key = extractKey(raw);
      if (key) {
        if (!ROOT_KEYS.has(key.toLowerCase())) {
          const col = raw.indexOf(key);
          diag(i, col, key.length, "error", `Unknown root key '${key}'.`);
        }
        if (key.toLowerCase() === "deck" || key.toLowerCase() === "stake") {
          const val = extractValue(raw);
          if (val) {
            const enumName = key.toLowerCase() === "deck" ? "MotelyDeck" : "MotelyStake";
            const members = Enums[enumName] ?? [];
            const stripped = val.replace(/\s+/g, "");
            if (!members.some((m) => m.toLowerCase() === stripped.toLowerCase())) {
              const col = raw.lastIndexOf(val);
              diag(i, col, val.length, "error", `Unknown ${key} '${val}'. Expected one of: ${members.join(", ")}.`);
            }
          }
        }
        if (SECTION_KEYS.has(key.toLowerCase())) inSection = true;
      }
      continue;
    }

    // List item directly under must/should/mustNot: consume the whole clause (recursing
    // into any nested and/or clauses:) and resume scanning after it.
    if (trimmed.startsWith("- ") && inSection) {
      i = consumeClauseAt(i, ind) - 1; // the for-loop's i++ lands exactly on the next line
      continue;
    }
  }

  return diags;
}
