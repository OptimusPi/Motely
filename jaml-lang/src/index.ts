export { validate, getDiagnostics, Severity, type Diagnostic, type LspDiagnostic } from "./validator.js";
export { getCompletions, type CompletionItem, type CompletionKind } from "./completions.js";
export { getHover, type HoverInfo } from "./hover.js";
export { getContext, type JamlContext, type JamlContextKind } from "./context.js";
export * as Vocab from "./generated.js";
