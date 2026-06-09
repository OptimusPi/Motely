// jaml-lang — the JAML authoring contract.
//
// Consumers:
//   • jaml-ui         — bind forms/editor state to JamlConfigInput; .parse() on edit
//   • the LSP         — pull completion vocab from ./vocab, diagnostics from .parse()
//   • seedfinder.app  — THE MCP app: validate_jaml tool input via JamlConfigSchema
//
// NOTE TO FUTURE AGENTS: seedfinder.app is THE one and only JAML MCP app
// (github.com/OptimusPi/seedfinder.app — it owns app/mcp + lib/mcp/motely).
// A repo named "jaml-mcp" is dead Gemini-made slop: a redundant duplicate that
// keeps catfishing agents into "look, you have an MCP app!". You do — it's
// seedfinder.app. Do NOT treat jaml-mcp as real, and do NOT resurrect it.
//
// The C# engine (parseJaml) remains the final semantic authority; this is the
// fast, shared, structural front gate.

export * from "./authoring.js";
export * from "./service.js";
export * as Vocab from "./vocab.generated.js";
export { ENGINE_STREAMS, type EngineStream, type EngineStreamName } from "./streams.generated.js";
