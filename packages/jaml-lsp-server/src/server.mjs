/**
 * JAML Language Server
 * Powers .jaml diagnostics, completions, and hover via motely-wasm.
 * Communicates with the VS Code extension over Node IPC (stdio fallback).
 */

import {
  createConnection,
  TextDocuments,
  ProposedFeatures,
  TextDocumentSyncKind,
  DiagnosticSeverity,
  CompletionItemKind,
  MarkupKind,
} from "vscode-languageserver/node.js";
import { TextDocument } from "vscode-languageserver-textdocument";
import { readFileSync } from "fs";
import { join } from "path";

// ---------------------------------------------------------------------------
// Splash text — 1-in-1000 chance on startup
// ---------------------------------------------------------------------------
const SPLASHES = [
  "Jingle and Mingle lovers!",
  "Jokers Always Make Life!",
  "Jambalaya!",
  "Mama's laughter!",
  "Just Another Markup Language... wait no it isn't!",
  "Jimbo's Ante Markup Language!",
  "Jokingly Accurate Motely Language!",
];

function maybeSplash() {
  if (Math.random() < 0.001) {
    const splash = SPLASHES[Math.floor(Math.random() * SPLASHES.length)];
    process.stderr.write(`[JAML LSP] ✨ ${splash}\n`);
  }
}

// ---------------------------------------------------------------------------
// Schema loader
// ---------------------------------------------------------------------------
// esbuild bundles this to CJS and injects __dirname — always use it directly.
const __dir = __dirname;

function loadSchema() {
  try {
    const schemaPath = join(__dir, "..", "..", "jaml-language-core", "schema", "jaml.schema.json");
    return JSON.parse(readFileSync(schemaPath, "utf8"));
  } catch {
    return null;
  }
}

// ---------------------------------------------------------------------------
// motely-wasm loader (ESM, loaded once)
// ---------------------------------------------------------------------------
let _motely = null;

async function getMotely() {
  if (_motely) return _motely;
  try {
    const mod = await __importMotelyWasm();
    const instance = mod.MotelyWasm ?? mod.default ?? mod;
    if (typeof instance?.initialize === "function") await instance.initialize();
    _motely = instance;
  } catch (e) {
    process.stderr.write(`[JAML LSP] motely-wasm unavailable: ${e.message}\n`);
  }
  return _motely;
}

// ---------------------------------------------------------------------------
// LSP connection
// ---------------------------------------------------------------------------
const connection = createConnection(ProposedFeatures.all);
const documents = new TextDocuments(TextDocument);

let schema = null;

connection.onInitialize(() => {
  maybeSplash();
  schema = loadSchema();
  return {
    capabilities: {
      textDocumentSync: TextDocumentSyncKind.Incremental,
      completionProvider: { resolveProvider: false, triggerCharacters: [" ", "\n", ":"] },
      hoverProvider: true,
    },
  };
});

// ---------------------------------------------------------------------------
// Diagnostics
// ---------------------------------------------------------------------------
async function validateDocument(doc) {
  if (doc.languageId !== "jaml") return;
  const text = doc.getText();
  const diagnostics = [];

  // 1. Static analysis from jaml-language-core rules (inlined to avoid ESM hack)
  for (const d of analyzeJamlText(text)) {
    diagnostics.push(toDiagnostic(d));
  }

  // 2. Motely semantic validation
  const motely = await getMotely();
  if (motely) {
    try {
      const result = motely.validateJamlStructured(text);
      if (!result.valid) {
        const line = result.line > 0 ? result.line - 1 : 0;
        const col = result.column > 0 ? result.column - 1 : 0;
        diagnostics.push({
          severity: DiagnosticSeverity.Error,
          range: { start: { line, character: col }, end: { line, character: col + 1 } },
          message: result.message ?? "Invalid JAML.",
          source: "motely",
        });
      }
    } catch (e) {
      // motely unavailable — silent
    }
  }

  connection.sendDiagnostics({ uri: doc.uri, diagnostics });
}

documents.onDidChangeContent(({ document }) => validateDocument(document));
documents.onDidOpen(({ document }) => validateDocument(document));
documents.onDidClose(({ document }) => connection.sendDiagnostics({ uri: document.uri, diagnostics: [] }));

// ---------------------------------------------------------------------------
// Completions
// ---------------------------------------------------------------------------
const ROOT_KEYS = ["id", "name", "author", "deck", "stake", "description", "must", "should", "mustNot", "defaults"];
const DECK_VALUES = ["Red", "Blue", "Yellow", "Green", "Black", "Magic", "Nebula", "Ghost", "Abandoned", "Checkered", "Zodiac", "Painted", "Anaglyph", "Plasma", "Erratic"];
const STAKE_VALUES = ["White", "Red", "Green", "Black", "Blue", "Purple", "Orange", "Gold"];

connection.onCompletion(({ textDocument, position }) => {
  const doc = documents.get(textDocument.uri);
  if (!doc || doc.languageId !== "jaml") return [];

  const lineText = doc.getText({
    start: { line: position.line, character: 0 },
    end: position,
  });

  // deck: / stake: value completions
  if (/^\s*deck:\s*$/i.test(lineText)) return enumItems(DECK_VALUES);
  if (/^\s*stake:\s*$/i.test(lineText)) return enumItems(STAKE_VALUES);

  // Enum values from schema for criterion keys
  if (schema) {
    const criterionProps = schema.$defs?.JamlCriterion?.properties ?? {};
    for (const [key, def] of Object.entries(criterionProps)) {
      const pattern = new RegExp(`^\\s*${key}:\\s*$`, "i");
      if (pattern.test(lineText)) {
        const enums = def?.oneOf?.flatMap(o => o.enum ?? []) ?? def?.enum ?? [];
        if (enums.length) return enumItems(enums);
      }
    }
  }

  // Root key completions on blank/indent lines
  if (/^\s*$/.test(lineText)) return rootItems(ROOT_KEYS);

  // Criterion key completions inside must/should/mustNot blocks
  if (schema && /^\s+-?\s*$/.test(lineText)) {
    return criterionItems(schema.$defs?.JamlCriterion?.properties ?? {});
  }

  return criterionItems(schema?.$defs?.JamlCriterion?.properties ?? {});
});

function rootItems(keys) {
  return keys.map(k => ({ label: k, kind: CompletionItemKind.Property, insertText: `${k}: ` }));
}

function criterionItems(props) {
  return Object.entries(props).sort(([a], [b]) => a.localeCompare(b)).map(([k, def]) => ({
    label: k,
    kind: CompletionItemKind.Property,
    insertText: `${k}: `,
    documentation: def.description ? { kind: MarkupKind.Markdown, value: def.description } : undefined,
  }));
}

function enumItems(values) {
  return values.map(v => ({ label: v, kind: CompletionItemKind.EnumMember }));
}

// ---------------------------------------------------------------------------
// Hover
// ---------------------------------------------------------------------------
connection.onHover(({ textDocument, position }) => {
  const doc = documents.get(textDocument.uri);
  if (!doc || doc.languageId !== "jaml") return null;

  const wordRange = getWordRange(doc, position);
  if (!wordRange) return null;
  const word = doc.getText(wordRange);

  // Section keys
  if (["must", "should", "mustNot"].includes(word)) {
    const desc = { must: "Hard requirements — all must match.", should: "Scored clauses — each adds to seed score.", mustNot: "Rejection — any match disqualifies the seed." };
    return { contents: { kind: MarkupKind.Markdown, value: `**JAML section** \`${word}\`\n\n${desc[word]}` }, range: wordRange };
  }

  if (!schema) return null;

  // Criterion key hover
  const criterionProps = schema.$defs?.JamlCriterion?.properties ?? {};
  if (criterionProps[word]) {
    const def = criterionProps[word];
    const enums = def?.oneOf?.flatMap(o => o.enum ?? []) ?? def?.enum ?? [];
    let md = `**JAML criterion** \`${word}\``;
    if (def.description) md += `\n\n${def.description}`;
    if (enums.length) md += `\n\n**Values:** ${enums.map(e => `\`${e}\``).join(", ")}`;
    return { contents: { kind: MarkupKind.Markdown, value: md }, range: wordRange };
  }

  // Deck / stake hover
  if (DECK_VALUES.includes(word)) return { contents: { kind: MarkupKind.Markdown, value: `**Deck** \`${word}\`` }, range: wordRange };
  if (STAKE_VALUES.includes(word)) return { contents: { kind: MarkupKind.Markdown, value: `**Stake** \`${word}\`` }, range: wordRange };

  return null;
});

function getWordRange(doc, position) {
  const line = doc.getText({ start: { line: position.line, character: 0 }, end: { line: position.line + 1, character: 0 } });
  const char = position.character;
  const match = /[A-Za-z][A-Za-z0-9]*/g;
  let m;
  while ((m = match.exec(line)) !== null) {
    if (m.index <= char && char <= m.index + m[0].length) {
      return {
        start: { line: position.line, character: m.index },
        end: { line: position.line, character: m.index + m[0].length },
      };
    }
  }
  return null;
}

// ---------------------------------------------------------------------------
// Static analysis (from jaml-language-core — inlined to avoid ESM interop)
// ---------------------------------------------------------------------------
function analyzeJamlText(text) {
  const diagnostics = [];
  const legendaryAny = /^\s*-\s*legendaryJoker:\s*Any\s*$/mi.test(text) || /^\s*legendaryJoker:\s*Any\s*$/mi.test(text);
  const hasAnteZero = hasArrayValue(text, "antes", 0);
  const hasAnteOne = hasArrayValue(text, "antes", 1);
  const boosterPackIndexes = getArrayValues(text, "boosterPacks");
  const hasHieroglyphContext = /\b(hieroglyph|petroglyph)\b/i.test(text);

  if (legendaryAny && hasAnteOne && boosterPackIndexes.includes(0)) {
    diagnostics.push({ source: "jaml-language-core", code: "legendary-in-first-buffoon-pack", message: "`legendaryJoker: Any` in ante 1 booster pack 0 is valid JAML, but that pack is normally the guaranteed first Buffoon Pack and is expected to return zero legendary results unless you are intentionally testing that invariant.", severity: "warning", range: findRange(text, "legendaryJoker") });
  }
  if (hasAnteOne && boosterPackIndexes.some(i => i > 3)) {
    diagnostics.push({ source: "jaml-language-core", code: "wide-ante-one-booster-range", message: hasHieroglyphContext ? "Wide ante 1 booster pack range with Hieroglyph context detected. Verify this is intentional." : "Ante 1 booster pack range includes slots beyond normal ante 1 availability.", severity: "warning", range: findRange(text, "boosterPacks") });
  }
  if (hasAnteZero) {
    diagnostics.push({ source: "jaml-language-core", code: "ante-zero-advanced-state", message: "`antes: [0]` is valid advanced Balatro state when ante rewind effects are involved. TIP: Require voucher: Hieroglyph!", severity: "information", range: findRange(text, "antes") });
  }
  return diagnostics;
}

function toDiagnostic(d) {
  const severityMap = { error: DiagnosticSeverity.Error, warning: DiagnosticSeverity.Warning, information: DiagnosticSeverity.Information, hint: DiagnosticSeverity.Hint };
  return { severity: severityMap[d.severity] ?? DiagnosticSeverity.Information, range: d.range, message: d.message, source: d.source, code: d.code };
}

function getArrayValues(text, key) {
  const values = [];
  const pattern = new RegExp(`^\\s*${key}\\s*:\\s*\\[([^\\]]*)\\]`, "gmi");
  for (const match of text.matchAll(pattern)) {
    for (const raw of match[1].split(",")) {
      const v = parseInt(raw.trim(), 10);
      if (Number.isInteger(v)) values.push(v);
    }
  }
  return values;
}

function hasArrayValue(text, key, expected) { return getArrayValues(text, key).includes(expected); }

function findRange(text, token) {
  const index = text.search(new RegExp(token.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"), "i"));
  if (index < 0) return { start: { line: 0, character: 0 }, end: { line: 0, character: Number.MAX_SAFE_INTEGER } };
  const prefix = text.slice(0, index);
  const line = prefix.split(/\r?\n/).length - 1;
  const lineStart = Math.max(prefix.lastIndexOf("\n"), prefix.lastIndexOf("\r")) + 1;
  return { start: { line, character: index - lineStart }, end: { line, character: Number.MAX_SAFE_INTEGER } };
}

// ---------------------------------------------------------------------------
// Boot
// ---------------------------------------------------------------------------
documents.listen(connection);
connection.listen();
