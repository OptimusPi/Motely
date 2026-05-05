export type JamlPreviewSection = "must" | "should" | "mustNot";
export type JamlPreviewVisualType = "joker" | "consumable" | "voucher" | "tag" | "boss";

export interface JamlPreviewItem {
  id: string;
  section: JamlPreviewSection;
  clauseKey: string;
  visualType: JamlPreviewVisualType;
  value: string;
  source: string;
}

export type JamlPreviewGroups = Record<JamlPreviewSection, JamlPreviewItem[]>;

const CLAUSE_VISUAL_TYPES: Partial<Record<string, JamlPreviewVisualType>> = {
  joker: "joker",
  jokers: "joker",
  commonJoker: "joker",
  commonJokers: "joker",
  uncommonJoker: "joker",
  uncommonJokers: "joker",
  rareJoker: "joker",
  rareJokers: "joker",
  mixedJoker: "joker",
  mixedJokers: "joker",
  soulJoker: "joker",
  legendaryJoker: "joker",
  voucher: "voucher",
  vouchers: "voucher",
  tarot: "consumable",
  tarotCard: "consumable",
  spectral: "consumable",
  spectralCard: "consumable",
  planet: "consumable",
  planetCard: "consumable",
  boss: "boss",
  bosses: "boss",
  tag: "tag",
  tags: "tag",
  smallBlindTag: "tag",
  bigBlindTag: "tag",
};

function createEmptyGroups(): JamlPreviewGroups {
  return { must: [], should: [], mustNot: [] };
}

function stripWrappingQuotes(value: string): string {
  if (
    (value.startsWith('"') && value.endsWith('"')) ||
    (value.startsWith("'") && value.endsWith("'"))
  ) {
    return value.slice(1, -1).trim();
  }

  return value;
}

function normalizeCandidateValue(value: string): string | null {
  const normalized = stripWrappingQuotes(value.trim()).replace(/,$/, "").trim();
  if (!normalized) return null;
  if (normalized === "Any") return null;
  return normalized;
}

function parseInlineValues(raw: string): string[] {
  const trimmed = raw.trim();
  if (!trimmed) return [];

  if (trimmed.startsWith("[") && trimmed.includes("]")) {
    const body = trimmed.slice(1, trimmed.indexOf("]"));
    return body
      .split(",")
      .map((value) => normalizeCandidateValue(value))
      .filter((value): value is string => Boolean(value));
  }

  const normalized = normalizeCandidateValue(trimmed);
  return normalized ? [normalized] : [];
}

export function extractVisualJamlItems(jaml: string): JamlPreviewGroups {
  const groups = createEmptyGroups();
  const seen = new Set<string>();
  const safeJaml = jaml || "";
  const lines = safeJaml.replace(/\r\n/g, "\n").split("\n");

  let currentSection: JamlPreviewSection | null = null;

  for (const rawLine of lines) {
    const trimmed = rawLine.trim();
    if (!trimmed || trimmed.startsWith("#")) continue;

    const sectionMatch = /^(must|should|mustNot):\s*$/.exec(trimmed);
    if (sectionMatch) {
      currentSection = sectionMatch[1] as JamlPreviewSection;
      continue;
    }

    const indent = rawLine.search(/\S|$/);
    if (indent === 0 && /^[A-Za-z][A-Za-z0-9]*:\s*/.test(trimmed)) {
      currentSection = null;
    }

    if (!currentSection) continue;

    const clauseMatch = /^-?\s*([A-Za-z][A-Za-z0-9]*):\s*(.*?)\s*$/.exec(trimmed);
    if (!clauseMatch) continue;

    const clauseKey = clauseMatch[1];
    const visualType = CLAUSE_VISUAL_TYPES[clauseKey];
    if (!visualType) continue;

    const values = parseInlineValues(clauseMatch[2]);
    for (const value of values) {
      const dedupeKey = `${currentSection}:${visualType}:${value.toLowerCase()}`;
      if (seen.has(dedupeKey)) continue;
      seen.add(dedupeKey);
      groups[currentSection].push({
        id: dedupeKey,
        section: currentSection,
        clauseKey,
        visualType,
        value,
        source: rawLine,
      });
    }
  }

  return groups;
}
