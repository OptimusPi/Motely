import type { Diagnostic } from "@codemirror/lint";
/**
 * Lints JAML source for CodeMirror. Combines jaml-lang's structural
 * validation (root/discriminator/clause keys, enum values) with the engine's
 * own MotelyJaml.validate/validateLine, which catches whole-document and
 * JUMMY one-line clause errors jaml-lang's lightweight walker doesn't.
 */
export declare function jamlLinter(source: string): Promise<Diagnostic[]>;
//# sourceMappingURL=jamlLinter.d.ts.map