import { parse as parseYaml } from "yaml";

export type JamlClauseKind = "must" | "should" | "mustNot";

export type JamlItemType =
  | "joker"
  | "voucher"
  | "boss"
  | "tag"
  | "tarot"
  | "planet"
  | "spectral"
  | "consumable"
  | "standardcard"
  | "deck"
  | "stake"
  | "seed"
  | "any"
  | "unknown";

export interface ParsedJamlClause {
  kind: JamlClauseKind;
  itemType: JamlItemType;
  /** Exact names as written in JAML (e.g. "WeeJoker"). */
  names: string[];
  /** Optional explicit ante window. */
  antes?: number[];
  /** Score weight for should clauses; 0 for must/mustNot. */
  score: number;
  /** Optional user-facing label; falls back to a generated one. */
  label: string;
  /** Raw clause object for advanced consumers. */
  raw: Record<string, unknown>;
}

const ITEM_KEYS: JamlItemType[] = [
  "joker",
  "voucher",
  "boss",
  "tag",
  "tarot",
  "planet",
  "spectral",
  "consumable",
  "standardcard",
  "deck",
  "stake",
  "seed",
];

function asArray<T>(value: unknown): T[] {
  if (value === undefined || value === null) return [];
  if (Array.isArray(value)) return value as T[];
  return [value as T];
}

function toStringArray(value: unknown): string[] {
  return asArray<string>(value).filter((v): v is string => typeof v === "string");
}

function extractNames(clause: Record<string, unknown>): { itemType: JamlItemType; names: string[] } {
  for (const key of ITEM_KEYS) {
    if (key in clause) {
      return { itemType: key, names: toStringArray(clause[key]) };
    }
  }
  // "any" is a wildcard catch-all supported by some JAML dialects.
  if ("any" in clause) return { itemType: "any", names: [] };
  return { itemType: "unknown", names: [] };
}

function parseClauseList(kind: JamlClauseKind, list: unknown): ParsedJamlClause[] {
  if (!Array.isArray(list)) return [];
  return list
    .filter((c): c is Record<string, unknown> => c !== null && typeof c === "object")
    .map((clause) => {
      const { itemType, names } = extractNames(clause);
      const antes = Array.isArray(clause.antes)
        ? clause.antes.filter((n): n is number => typeof n === "number").map((n) => Math.floor(n))
        : undefined;
      const score = typeof clause.score === "number" ? clause.score : 0;
      const label = typeof clause.label === "string" && clause.label.trim()
        ? clause.label
        : names.length > 0
          ? `${itemType}: ${names.join(", ")}`
          : `${itemType}`;
      return {
        kind,
        itemType,
        names,
        antes,
        score,
        label,
        raw: clause,
      };
    })
    .filter((c) => c.itemType !== "unknown" || c.raw.label);
}

export interface ParsedJamlFilters {
  deck?: string;
  stake?: string;
  must: ParsedJamlClause[];
  should: ParsedJamlClause[];
  mustNot: ParsedJamlClause[];
  all: ParsedJamlClause[];
}

export function parseJamlClauses(jamlText: string): ParsedJamlFilters {
  let doc: Record<string, unknown> = {};
  try {
    doc = parseYaml(jamlText) as Record<string, unknown> || {};
  } catch {
    // Invalid YAML is handled gracefully; the engine will reject it later.
  }

  const must = parseClauseList("must", doc.must);
  const should = parseClauseList("should", doc.should);
  const mustNot = parseClauseList("mustNot", doc.mustNot);

  return {
    deck: typeof doc.deck === "string" ? doc.deck : undefined,
    stake: typeof doc.stake === "string" ? doc.stake : undefined,
    must,
    should,
    mustNot,
    all: [...must, ...should, ...mustNot],
  };
}

export function splitCamelCase(key: string): string {
  return key.replace(/([A-Z])/g, " $1").trim();
}

export function normalizeName(name: string): string {
  return splitCamelCase(name).toLowerCase().replace(/[^a-z0-9]/g, "");
}

export function matchClauseToItem(
  clause: ParsedJamlClause,
  itemType: JamlItemType,
  itemName: string
): boolean {
  if (clause.itemType !== "any" && clause.itemType !== itemType) return false;
  if (clause.names.length === 0) return clause.itemType === "any";
  const needle = normalizeName(itemName);
  return clause.names.some((n) => normalizeName(n) === needle);
}

export function matchClauseToAnte(clause: ParsedJamlClause, ante: number): boolean {
  if (!clause.antes || clause.antes.length === 0) return true;
  return clause.antes.includes(ante);
}

export function highlightClassForKind(kind: JamlClauseKind): string {
  switch (kind) {
    case "must":
      return "j-glow--must";
    case "should":
      return "j-glow--should";
    case "mustNot":
      return "j-glow--match";
    default:
      return "";
  }
}

/** Convenience matcher that maps the coarse decoder category to a JAML item type. */
export function matchMotelyItemToClause(
  item: { category?: string; displayName?: string },
  clause: ParsedJamlClause
): boolean {
  if (!item.category || !item.displayName) return false;
  const category = item.category as JamlItemType | "playing" | "consumable" | "unknown";
  const itemType: JamlItemType = category === "playing" ? "standardcard" : category === "unknown" ? "unknown" : category;
  return matchClauseToItem(clause, itemType, item.displayName);
}
