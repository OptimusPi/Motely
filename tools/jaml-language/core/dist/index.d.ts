export declare const JAML_LANGUAGE_ID = "jaml";
export declare const JUMMY_LANGUAGE_ID = "jummy";
export declare const JAML_ROOT_KEYS: readonly string[];
export declare const CLAUSE_KEYS: readonly string[];
export declare function looksLikeJson(text: string): boolean;
export declare function looksLikeJummy(text: string): boolean;
export declare function unknownRootKeys(root: Record<string, unknown>): string[];
