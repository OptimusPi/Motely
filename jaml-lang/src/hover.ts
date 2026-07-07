import { Enums, DiscriminatorValueEnum, DiscriminatorSourceKeys, DiscriminatorClauseKeys, ClauseKeyValueEnum } from "./generated.js";
import { getContext } from "./context.js";

// Clause-level key -> enum name, case-insensitive. Single source: generated ClauseKeyValueEnum.
const clauseValueEnum = new Map(
  Object.entries(ClauseKeyValueEnum).map(([k, v]) => [k.toLowerCase(), v]),
);

export interface HoverInfo {
  markdown: string;
}

export function getHover(text: string, offset: number): HoverInfo | null {
  const ctx = getContext(text, offset);

  if (ctx.kind === "discriminator" || ctx.kind === "discriminator-value") {
    const disc = ctx.discriminator ?? ctx.valueKey ?? "";
    const enumName = disc ? DiscriminatorValueEnum[disc] : null;
    const clauseKeys = disc ? DiscriminatorClauseKeys[disc] : null;
    const sourceKeys = disc ? DiscriminatorSourceKeys[disc] : null;

    let md = `**${disc || "clause discriminator"}**`;
    if (enumName) md += `\n\nValue: \`${enumName}\` enum`;
    if (clauseKeys?.length) md += `\n\nAllowed keys: ${clauseKeys.map((k) => `\`${k}\``).join(", ")}`;
    if (sourceKeys?.length) md += `\n\nSource keys: ${sourceKeys.map((k) => `\`${k}\``).join(", ")}`;
    return { markdown: md };
  }

  if (ctx.kind === "clause-value" && ctx.valueKey) {
    const enumName = clauseValueEnum.get(ctx.valueKey.toLowerCase());
    if (enumName) {
      const members = Enums[enumName] ?? [];
      return { markdown: `**${ctx.valueKey}** — \`${enumName}\`\n\nValues: ${members.map((m) => `\`${m}\``).join(", ")}` };
    }
  }

  return null;
}
