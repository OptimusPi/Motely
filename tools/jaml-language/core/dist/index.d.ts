export declare const JAML_LANGUAGE_ID = "jaml";
export declare const JUMMY_LANGUAGE_ID = "jummy";
export declare const JAML_ROOT_KEYS: readonly ["id", "name", "author", "dateCreated", "description", "deck", "stake", "defaults", "must", "should", "mustNot", "aesthetics", "hashtags", "seeds"];
export declare const CLAUSE_KEYS: readonly ["joker", "rareJoker", "voucher", "boss", "tag", "tarotCard", "spectralCard", "planet", "or", "and", "label", "score", "mode", "sources", "antes"];
export declare function looksLikeJson(text: string): boolean;
export declare function looksLikeJummy(text: string): boolean;
export declare function unknownRootKeys(root: Record<string, unknown>): string[];
