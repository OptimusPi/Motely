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
      if (norm.toLowerCase() === "any") continue;
      if (members.some((m) => m.toLowerCase() === norm.toLowerCase())) continue;
      const col = rawLine.lastIndexOf(token);
      diag(lineIdx, col >= 0 ? col : ind, token.length, "warning", `Unknown ${enumName} value '${token}'.`);
    }
  }

  let inClause = false;
  let clauseIndent = -1;
  let clauseDiscriminator: string | null = null;
  let inSources = false;
  let sourcesIndent = -1;
  let inWith = false;
  let withIndent = -1;
  let inSection = false; // inside must/should/mustNot

  // A key is valid only relative to the clause's discriminator — generated
  // DiscriminatorClauseKeys is the sole authority; no flat union exists.
  function checkKeyAllowed(disc: string, key: string, lineIdx: number, raw: string, ind: number) {
    const allowed = DiscriminatorClauseKeys[disc];
    if (allowed && !allowed.some((k) => k.toLowerCase() === key.toLowerCase())) {
      const col = raw.indexOf(key, ind);
      diag(lineIdx, col, key.length, "error", `Key '${key}' is not valid for ${disc}.`);
    }
  }

  for (let i = 0; i < lines.length; i++) {
    const raw = lines[i];
    const trimmed = raw.trimStart();
    const ind = indentOf(raw);

    // Skip empty / comment lines
    if (!trimmed || trimmed.startsWith("#")) continue;

    // Root level (indent 0, not a list)
    if (ind === 0 && !trimmed.startsWith("-")) {
      inClause = false;
      inSources = false;
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
        if (["must", "should", "mustnot"].includes(key.toLowerCase())) inSection = true;
      }
      continue;
    }

    // List item: start of a new clause
    if (trimmed.startsWith("- ") && inSection) {
      const content = trimmed.slice(2).trimStart();
      clauseIndent = ind;
      inClause = true;
      inSources = false;
      inWith = false;
      clauseDiscriminator = findClauseDiscriminator(lines, i, ind);

      const key = extractKey("  " + content);
      if (key) {
        if (!DISC_SET.has(key.toLowerCase())) {
          if (clauseDiscriminator) {
            checkKeyAllowed(clauseDiscriminator, key, i, raw, ind);
          } else {
            const col = raw.indexOf(key, ind);
            diag(i, col, key.length, "error", `Clause has no discriminator (expected a clause type key like joker:, voucher:, tag:, ...).`);
          }
        } else {
          // Validate discriminator value
          const val = extractValue(raw);
          if (val) {
            const canon = Discriminators.find((d) => d.toLowerCase() === key.toLowerCase()) ?? key;
            const enumName = DiscriminatorValueEnum[canon];
            if (enumName) {
              const members = Enums[enumName] ?? [];
              const stripped = val.replace(/\s+/g, "");
              if (
                !stripped.toLowerCase().startsWith("[") && // skip arrays
                val !== "Any" &&
                !members.some((m) => m.toLowerCase() === stripped.toLowerCase())
              ) {
                const col = raw.lastIndexOf(val);
                diag(i, col, val.length, "warning", `Unknown ${enumName} value '${val}'.`);
              }
            }
          }
        }
      }
      continue;
    }

    // Keys inside a clause
    if (inClause && ind > clauseIndent) {
      const key = extractKey(raw);
      if (!key) continue;

      // entering sources block?
      if (key.toLowerCase() === "sources") {
        if (clauseDiscriminator) checkKeyAllowed(clauseDiscriminator, key, i, raw, ind);
        inSources = true;
        sourcesIndent = ind;
        continue;
      }

      if (inSources && ind > sourcesIndent) {
        // validate source key — prescan already canonicalized the discriminator
        if (clauseDiscriminator) {
          const allowed = DiscriminatorSourceKeys[clauseDiscriminator];
          if (allowed && !allowed.some((k) => k.toLowerCase() === key.toLowerCase())) {
            const col = raw.indexOf(key, ind);
            diag(i, col, key.length, "error", `Unknown source key '${key}' for ${clauseDiscriminator}.`);
          }
        }
        continue;
      }

      // Back out of sources if indent dropped
      if (inSources && ind <= sourcesIndent) {
        inSources = false;
      }

      // entering with block? The `with` key itself is gated per-discriminator;
      // with-modifier vocabulary has no codegen table yet, so inside the block
      // we stay silent rather than guess.
      if (key.toLowerCase() === "with") {
        if (clauseDiscriminator) checkKeyAllowed(clauseDiscriminator, key, i, raw, ind);
        inWith = true;
        withIndent = ind;
        continue;
      }
      if (inWith && ind > withIndent) continue;
      if (inWith && ind <= withIndent) inWith = false;

      // Discriminator line (may appear after other keys; prescan already resolved
      // the clause's discriminator) — validate its value only
      if (DISC_SET.has(key.toLowerCase())) {
        const val = extractValue(raw);
        if (val) {
          const canon = Discriminators.find((d) => d.toLowerCase() === key.toLowerCase()) ?? key;
          const enumName = DiscriminatorValueEnum[canon];
          if (enumName) {
            const members = Enums[enumName] ?? [];
            const stripped = val.replace(/\s+/g, "");
            if (
              !stripped.startsWith("[") &&
              val !== "Any" &&
              !members.some((m) => m.toLowerCase() === stripped.toLowerCase())
            ) {
              const col = raw.lastIndexOf(val);
              diag(i, col, val.length, "warning", `Unknown ${enumName} value '${val}'.`);
            }
          }
        }
        continue;
      }

      // Gate against the discriminator's allowed keys — the only valid question.
      // A clause with no discriminator was already flagged at its list item.
      if (clauseDiscriminator) {
        checkKeyAllowed(clauseDiscriminator, key, i, raw, ind);
      }

      // Enum-constrained value check (seal/rank/edition/suit/enhancement/stickers).
      checkClauseValueEnum(key, raw, i, ind);
    }
  }

  return diags;
}
