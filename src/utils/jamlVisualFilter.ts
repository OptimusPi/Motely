/**
 * Utilities for converting between JAML text and JamlVisualFilter.
 *
 * Intentionally does NOT depend on a YAML library — uses the same
 * line-by-line approach as jamlMapPreview.ts to stay zero-dep.
 */

import type { JamlVisualClause, JamlVisualFilter, JamlZone } from "../components/JamlIdeVisual.js";

// ─── Text → Filter ────────────────────────────────────────────────────────────

function stripQuotes(s: string): string {
  if ((s.startsWith('"') && s.endsWith('"')) || (s.startsWith("'") && s.endsWith("'"))) {
    return s.slice(1, -1).trim();
  }
  return s;
}

function parseScalarValue(raw: string): string | null {
  const v = stripQuotes(raw.trim().replace(/,$/, "").trim());
  return v || null;
}

function parseInlineList(raw: string): string[] {
  const t = raw.trim();
  if (t.startsWith("[") && t.includes("]")) {
    const body = t.slice(1, t.indexOf("]"));
    return body
      .split(",")
      .map((s) => parseScalarValue(s))
      .filter((s): s is string => s !== null);
  }
  const v = parseScalarValue(t);
  return v ? [v] : [];
}

function topLevelScalar(lines: string[], key: string): string | undefined {
  for (const line of lines) {
    const m = new RegExp(`^${key}:\\s*(.+)$`).exec(line.trim());
    if (m) return stripQuotes(m[1].trim());
  }
  return undefined;
}

const CLAUSE_ZONE_KEYS: Set<string> = new Set([
  "joker", "jokers", "commonJoker", "commonJokers", "uncommonJoker", "uncommonJokers",
  "rareJoker", "rareJokers", "mixedJoker", "mixedJokers", "soulJoker", "legendaryJoker",
  "voucher", "vouchers",
  "tarot", "tarotCard", "spectral", "spectralCard", "planet", "planetCard",
  "boss", "bosses",
  "tag", "tags", "smallBlindTag", "bigBlindTag", "smallblindtag", "bigblindtag",
]);

// JAML uses "mustnot" as zone key in some contexts; the visual filter uses "mustnot".
// The text format may use "mustnot" or "must_not" — handle both, normalise to "mustnot".
function sectionToZone(raw: string): JamlZone | null {
  if (raw === "must") return "must";
  if (raw === "should") return "should";
  if (raw === "mustnot" || raw === "must_not" || raw === "mustNot") return "mustnot";
  return null;
}

let _uid = 0;
function uid(): string {
  return `clause-${++_uid}`;
}

interface ClauseAccum {
  type: string;
  value: string;
  antes?: number[];
  boosterPacks?: number[];
  score?: number;
  edition?: string;
}

export function jamlTextToVisualFilter(text: string): JamlVisualFilter {
  const lines = text.replace(/\r\n/g, "\n").split("\n");
  const filter: JamlVisualFilter = { must: [], should: [], mustnot: [] };
  filter.name = topLevelScalar(lines, "name");
  filter.author = topLevelScalar(lines, "author");
  filter.description = topLevelScalar(lines, "description");
  filter.deck = topLevelScalar(lines, "deck");
  filter.stake = topLevelScalar(lines, "stake");

  let zone: JamlZone | null = null;
  let current: ClauseAccum | null = null;
  const seen = new Set<string>();

  function flushClause() {
    if (!current || !zone) return;
    const dedupeKey = `${zone}:${current.type}:${current.value.toLowerCase()}`;
    if (!seen.has(dedupeKey)) {
      seen.add(dedupeKey);
      const clause: JamlVisualClause = { id: uid(), type: current.type, value: current.value };
      if (current.antes && current.antes.length > 0) clause.antes = current.antes;
      if (current.boosterPacks && current.boosterPacks.length > 0) clause.boosterPacks = current.boosterPacks;
      if (current.score !== undefined) clause.score = current.score;
      if (current.edition) clause.edition = current.edition;
      clause.label = current.value;
      filter[zone].push(clause);
    }
    current = null;
  }

  for (const rawLine of lines) {
    const trimmed = rawLine.trim();
    if (!trimmed || trimmed.startsWith("#")) continue;

    // Top-level section header: must:/should:/mustnot:
    const sectionMatch = /^(must|should|mustnot|must_not|mustNot):\s*$/.exec(trimmed);
    if (sectionMatch) {
      flushClause();
      zone = sectionToZone(sectionMatch[1]);
      continue;
    }

    // Top-level key (non-section) resets zone
    const indent = rawLine.search(/\S|$/);
    if (indent === 0 && /^[A-Za-z]/.test(trimmed) && !trimmed.startsWith("-")) {
      flushClause();
      if (!sectionToZone(trimmed.replace(/:.*/, ""))) zone = null;
      continue;
    }

    if (!zone) continue;

    // New clause start: "  - rareJoker: Blueprint"
    const clauseStart = /^-\s*([A-Za-z][A-Za-z0-9]*):\s*(.*?)\s*$/.exec(trimmed);
    if (clauseStart) {
      flushClause();
      const type = clauseStart[1];
      const rawVal = clauseStart[2];
      if (!CLAUSE_ZONE_KEYS.has(type)) continue;
      const value = parseScalarValue(rawVal) ?? "Any";
      current = { type, value };
      continue;
    }

    // Continuation line inside a clause: "    antes: [1,2,3]"
    if (current && indent > 0 && !trimmed.startsWith("-")) {
      const contMatch = /^([A-Za-z_][A-Za-z0-9_]*):\s*(.*?)\s*$/.exec(trimmed);
      if (contMatch) {
        const key = contMatch[1];
        const val = contMatch[2];
        if (key === "antes") {
          const nums = parseInlineList(val)
            .map(Number)
            .filter((n) => !isNaN(n));
          current.antes = nums;
        } else if (key === "boosterPacks") {
          const nums = parseInlineList(val)
            .map(Number)
            .filter((n) => !isNaN(n));
          current.boosterPacks = nums;
        } else if (key === "score") {
          const n = Number(val);
          if (!isNaN(n)) current.score = n;
        } else if (key === "edition") {
          current.edition = parseScalarValue(val) ?? undefined;
        } else if (key === "sources") {
          // handled by subsequent nested lines like `boosterPacks: [0, 1]`
        }
      }
    }
  }

  flushClause();
  return filter;
}

// ─── Filter → Text ───────────────────────────────────────────────────────────

function q(s: string | undefined): string {
  if (!s) return "";
  return /[:#\[\]{}|>&*!,'"?]/.test(s) ? `"${s.replace(/"/g, '\\"')}"` : s;
}

function serializeClause(clause: JamlVisualClause): string {
  let out = `  - ${clause.type}: ${q(clause.value)}\n`;
  if (clause.antes && clause.antes.length > 0) {
    out += `    antes: [${clause.antes.join(", ")}]\n`;
  }
  if (clause.boosterPacks && clause.boosterPacks.length > 0) {
    out += `    sources:\n`;
    out += `      boosterPacks: [${clause.boosterPacks.join(", ")}]\n`;
  }
  if (clause.score !== undefined) {
    out += `    score: ${clause.score}\n`;
  }
  if (clause.edition) {
    out += `    edition: ${q(clause.edition)}\n`;
  }
  return out;
}

export function visualFilterToJamlText(filter: JamlVisualFilter): string {
  const parts: string[] = [];
  if (filter.name) parts.push(`name: ${q(filter.name)}`);
  if (filter.author) parts.push(`author: ${q(filter.author)}`);
  if (filter.description) parts.push(`description: ${q(filter.description)}`);
  if (filter.deck) parts.push(`deck: ${q(filter.deck)}`);
  if (filter.stake) parts.push(`stake: ${q(filter.stake)}`);

  const zones: Array<{ key: string; label: string; clauses: JamlVisualClause[] }> = [
    { key: "must", label: "must", clauses: filter.must },
    { key: "should", label: "should", clauses: filter.should },
    { key: "mustnot", label: "mustnot", clauses: filter.mustnot },
  ];

  for (const { label, clauses } of zones) {
    if (clauses.length > 0) {
      parts.push(`${label}:`);
      for (const c of clauses) {
        parts.push(serializeClause(c).trimEnd());
      }
    }
  }

  return parts.join("\n") + "\n";
}
