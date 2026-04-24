import { clauseKeys, rootKeys } from "@jaml/schema";

export const JAML_LANGUAGE_ID = "jaml";
export const JUMMY_LANGUAGE_ID = "jummy";

export const JAML_ROOT_KEYS = rootKeys;
export const CLAUSE_KEYS = clauseKeys;

export function looksLikeJson(text: string): boolean {
  const t = text.trimStart();
  return t.startsWith("{") || t.startsWith("[");
}

export function looksLikeJummy(text: string): boolean {
  const t = text.toLowerCase();
  return t.includes("jummy:") || t.includes("what:") || t.includes("where:");
}

export function unknownRootKeys(root: Record<string, unknown>): string[] {
  const allowed = new Set<string>(JAML_ROOT_KEYS as readonly string[]);
  return Object.keys(root).filter((k) => !allowed.has(k));
}