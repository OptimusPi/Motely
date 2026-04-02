export const JAML_LANGUAGE_ID = "jaml";
export const JUMMY_LANGUAGE_ID = "jummy";
export const JAML_ROOT_KEYS = [
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
    "aesthetics",
    "hashtags",
    "seeds",
];
export const CLAUSE_KEYS = [
    "joker",
    "rareJoker",
    "voucher",
    "boss",
    "tag",
    "tarotCard",
    "spectralCard",
    "planet",
    "or",
    "and",
    "label",
    "score",
    "mode",
    "sources",
    "antes",
];
export function looksLikeJson(text) {
    const t = text.trimStart();
    return t.startsWith("{") || t.startsWith("[");
}
export function looksLikeJummy(text) {
    const t = text.toLowerCase();
    return t.includes("jummy:") || t.includes("what:") || t.includes("where:");
}
export function unknownRootKeys(root) {
    const allowed = new Set(JAML_ROOT_KEYS);
    return Object.keys(root).filter((k) => !allowed.has(k));
}
