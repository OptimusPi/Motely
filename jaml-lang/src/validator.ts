import {
  Discriminators, RootKeys, Enums,
  DiscriminatorValueEnum, DiscriminatorClauseKeys, DiscriminatorSourceKeys,
  ClauseKeyValueEnum, AllClauseLevelKeys,
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
const CLAUSE_KEYS = new Set(AllClauseLevelKeys.map((k) => k.toLowerCase()));
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
  let inSection = false; // inside must/should/mustNot

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
      clauseDiscriminator = null;
      inClause = true;
      inSources = false;

      const key = extractKey("  " + content);
      if (key) {
        if (!DISC_SET.has(key.toLowerCase()) && !CLAUSE_KEYS.has(key.toLowerCase())) {
          const col = raw.indexOf(key, ind);
          diag(i, col, key.length, "error", `Unknown key '${key}'.`);
        } else if (DISC_SET.has(key.toLowerCase())) {
          clauseDiscriminator = key;
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
        inSources = true;
        sourcesIndent = ind;
        continue;
      }

      if (inSources && ind > sourcesIndent) {
        // validate source key
        if (clauseDiscriminator) {
          const canon = Discriminators.find((d) => d.toLowerCase() === clauseDiscriminator!.toLowerCase()) ?? clauseDiscriminator;
          const allowed = DiscriminatorSourceKeys[canon];
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

      if (!DISC_SET.has(key.toLowerCase()) && !CLAUSE_KEYS.has(key.toLowerCase())) {
        const col = raw.indexOf(key, ind);
        diag(i, col, key.length, "error", `Unknown clause key '${key}'.`);
        continue;
      }

      // If it's a discriminator, update the current one
      if (DISC_SET.has(key.toLowerCase())) {
        clauseDiscriminator = key;
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

      // Check it's allowed for this discriminator
      if (clauseDiscriminator) {
        const canon = Discriminators.find((d) => d.toLowerCase() === clauseDiscriminator!.toLowerCase()) ?? clauseDiscriminator;
        const allowed = DiscriminatorClauseKeys[canon];
        if (allowed && !allowed.some((k) => k.toLowerCase() === key.toLowerCase())) {
          const col = raw.indexOf(key, ind);
          diag(i, col, key.length, "warning", `Key '${key}' is not valid for ${clauseDiscriminator}.`);
        }
      }

      // Enum-constrained value check (seal/rank/edition/suit/enhancement/stickers).
      checkClauseValueEnum(key, raw, i, ind);
    }
  }

  return diags;
}
