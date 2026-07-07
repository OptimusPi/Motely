import {
  Discriminators, RootKeys, RootValueEnums,
  Enums, DiscriminatorValueEnum, DiscriminatorClauseKeys, DiscriminatorSourceKeys,
  ClauseKeyValueEnum,
} from "./generated.js";
import { getContext } from "./context.js";

export type CompletionKind = "field" | "enum" | "value" | "keyword";

export interface CompletionItem {
  label: string;
  kind: CompletionKind;
  detail?: string;
  documentation?: string;
  insertText?: string;
}

/** Filter items whose label starts with prefix (case-insensitive, spaces stripped). */
function filterPrefix(items: CompletionItem[], prefix: string): CompletionItem[] {
  if (!prefix) return items;
  const lp = prefix.toLowerCase().replace(/\s+/g, "");
  return items.filter(
    (i) =>
      i.label.toLowerCase().startsWith(lp) ||
      i.label.toLowerCase().replace(/\s+/g, "").startsWith(lp),
  );
}

function kwItems(keys: readonly string[], kind: CompletionKind = "field"): CompletionItem[] {
  return keys.map((k) => ({ label: k, kind, insertText: `${k}: ` }));
}

function enumItems(enumName: string, detail?: string): CompletionItem[] {
  const members = Enums[enumName] ?? [];
  return members.map((m) => ({ label: m, kind: "enum" as const, detail: detail ?? enumName }));
}

// Clause-level key -> enum name, case-insensitive. Single source: generated ClauseKeyValueEnum.
const clauseValueEnum = new Map(
  Object.entries(ClauseKeyValueEnum).map(([k, v]) => [k.toLowerCase(), v]),
);

/** Normalise discriminator to the canonical casing expected by the vocab tables. */
function canonDisc(disc: string): string {
  return (
    Discriminators.find((d) => d.toLowerCase() === disc.toLowerCase()) ?? disc
  );
}

/**
 * Return completions for the given text at the given cursor offset.
 * Context-aware: only suggests what is physically valid at this position.
 */
export function getCompletions(text: string, offset: number): CompletionItem[] {
  const ctx = getContext(text, offset);

  switch (ctx.kind) {
    case "root-key":
      return filterPrefix(kwItems(RootKeys, "keyword"), ctx.prefix);

    case "root-value": {
      const enumName = RootValueEnums[ctx.valueKey ?? ""];
      if (enumName) return filterPrefix(enumItems(enumName), ctx.prefix);
      return [];
    }

    case "discriminator":
      return filterPrefix(
        Discriminators.map((d) => ({ label: d, kind: "keyword" as const, detail: "clause type", insertText: `${d}: ` })),
        ctx.prefix,
      );

    case "discriminator-value": {
      const disc = canonDisc(ctx.discriminator ?? "");
      const enumName = DiscriminatorValueEnum[disc];
      if (!enumName) return [];
      const items = enumItems(enumName);
      items.unshift({ label: "Any", kind: "enum", detail: "wildcard" });
      return filterPrefix(items, ctx.prefix);
    }

    case "clause-key": {
      const disc = canonDisc(ctx.discriminator ?? "");
      const allowed = DiscriminatorClauseKeys[disc] ?? [];
      return filterPrefix(kwItems(allowed), ctx.prefix);
    }

    case "clause-value": {
      const key = (ctx.valueKey ?? "").toLowerCase();
      // Enum-valued clause keys (edition/enhancement/seal/suit/rank/stickers) come straight
      // from the generated ClauseKeyValueEnum — same source the validator uses, no local copy.
      const enumName = clauseValueEnum.get(key);
      if (enumName) return filterPrefix(enumItems(enumName), ctx.prefix);
      if (key === "soulcardonly") return filterPrefix([
        { label: "true", kind: "value" }, { label: "false", kind: "value" }
      ], ctx.prefix);
      return [];
    }

    case "source-key": {
      const disc = canonDisc(ctx.discriminator ?? "");
      const allowed = DiscriminatorSourceKeys[disc] ?? [];
      return filterPrefix(kwItems(allowed), ctx.prefix);
    }

    case "source-value":
      return filterPrefix([
        { label: "true", kind: "value" }, { label: "false", kind: "value" }
      ], ctx.prefix);

    default:
      return [];
  }
}
