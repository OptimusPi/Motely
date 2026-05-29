// @motely/jaml-lang — the JAML authoring contract.
//
// Consumers:
//   • jaml-ui      — bind forms/editor state to JamlConfigInput; .parse() on edit
//   • the LSP      — pull completion vocab from ./vocab, diagnostics from .parse()
//   • the MCP app  — validate_jaml tool input via JamlConfigSchema
//
// The C# engine (parseJaml) remains the final semantic authority; this is the
// fast, shared, structural front gate.

export * from "./authoring.js";
export * from "./service.js";
export * as Vocab from "./vocab.generated.js";
