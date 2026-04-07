import {
  CompletionItemKind,
  createConnection,
  Diagnostic,
  DiagnosticSeverity,
  DidChangeConfigurationNotification,
  Hover,
  InitializeParams,
  InitializeResult,
  MarkupKind,
  ProposedFeatures,
  TextDocumentPositionParams,
  TextDocumentSyncKind,
} from "vscode-languageserver/node.js";
import { TextDocument } from "vscode-languageserver-textdocument";
import { parse as parseYaml } from "yaml";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import {
  CLAUSE_KEYS,
  JAML_ROOT_KEYS,
  looksLikeJson,
  unknownRootKeys,
} from "@motely/jaml-language-core";

// ── Schema-driven value data ────────────────────────────────────────────────

/** Map from JAML key → list of valid enum values, built from jaml.schema.json. */
const VALUE_MAP = new Map<string, string[]>();

function loadSchemaValues(): void {
  // Schema lives next to the bundled server.js (esbuild copies it during build).
  // Fallback: repo root (dev mode).
  const here = __dirname;
  const candidates = [
    resolve(here, "jaml.schema.json"),
    resolve(here, "..", "jaml.schema.json"),
    resolve(here, "..", "..", "..", "jaml.schema.json"),
  ];

  let schema: any;
  for (const p of candidates) {
    try {
      schema = JSON.parse(readFileSync(p, "utf8"));
      break;
    } catch {}
  }
  if (!schema) return;

  // Top-level enum fields (deck, stake)
  const props = schema.properties ?? {};
  for (const [key, def] of Object.entries<any>(props)) {
    if (def.enum) VALUE_MAP.set(key, def.enum);
  }

  // Clause-level fields inside must/should/mustNot items
  const clauseDefs =
    props.must?.items?.properties ??
    props.should?.items?.properties ??
    {};
  for (const [key, def] of Object.entries<any>(clauseDefs)) {
    if (def.enum && !VALUE_MAP.has(key)) {
      VALUE_MAP.set(key, def.enum);
    }
    // Also handle array-of-enum (e.g. jokers: [joker1, joker2])
    if (def.items?.enum && !VALUE_MAP.has(key)) {
      VALUE_MAP.set(key, def.items.enum);
    }
  }

  // Aesthetics
  if (props.aesthetics?.items?.enum) {
    VALUE_MAP.set("aesthetics", props.aesthetics.items.enum);
  }
}

loadSchemaValues();

// ── Helpers ─────────────────────────────────────────────────────────────────

const connection = createConnection(ProposedFeatures.all);
const documents = new Map<string, TextDocument>();

/** Detect which JAML key the cursor is on: returns the key if the line is `key: <cursor>`. */
function getKeyAtLine(line: string): string | null {
  const m = line.match(/^\s*(\w[\w-]*):\s*/);
  return m ? m[1] : null;
}

/** Find the word under the cursor position. */
function getWordAt(line: string, char: number): string {
  let start = char;
  let end = char;
  while (start > 0 && /[\w-]/.test(line[start - 1])) start--;
  while (end < line.length && /[\w-]/.test(line[end])) end++;
  return line.slice(start, end);
}

// ── Diagnostics ─────────────────────────────────────────────────────────────

function diagnosticsForDocument(text: string): Diagnostic[] {
  const diagnostics: Diagnostic[] = [];
  const max = Math.max(0, text.length - 1);

  try {
    let root: unknown;
    if (looksLikeJson(text)) {
      root = JSON.parse(text);
    } else {
      root = parseYaml(text);
    }

    if (!root || typeof root !== "object" || Array.isArray(root)) {
      diagnostics.push({
        severity: DiagnosticSeverity.Error,
        range: { start: { line: 0, character: 0 }, end: { line: 0, character: max } },
        message: "JAML root must be an object/mapping.",
        source: "jaml-lsp",
      });
      return diagnostics;
    }

    for (const bad of unknownRootKeys(root as Record<string, unknown>)) {
      diagnostics.push({
        severity: DiagnosticSeverity.Warning,
        range: { start: { line: 0, character: 0 }, end: { line: 0, character: max } },
        message: `Unknown root key '${bad}'.`,
        source: "jaml-lsp",
      });
    }
  } catch (error) {
    diagnostics.push({
      severity: DiagnosticSeverity.Error,
      range: { start: { line: 0, character: 0 }, end: { line: 0, character: max } },
      message: `Parse error: ${(error as Error).message}`,
      source: "jaml-lsp",
    });
  }

  return diagnostics;
}

// ── Lifecycle ───────────────────────────────────────────────────────────────

connection.onInitialize((_params: InitializeParams): InitializeResult => {
  return {
    capabilities: {
      textDocumentSync: TextDocumentSyncKind.Full,
      completionProvider: { resolveProvider: false, triggerCharacters: [":"] },
      hoverProvider: true,
    },
  };
});

connection.onInitialized(() => {
  connection.client.register(DidChangeConfigurationNotification.type, undefined);
});

connection.onDidOpenTextDocument((evt) => {
  const doc = TextDocument.create(
    evt.textDocument.uri,
    evt.textDocument.languageId,
    evt.textDocument.version,
    evt.textDocument.text
  );
  documents.set(doc.uri, doc);
  connection.sendDiagnostics({ uri: doc.uri, diagnostics: diagnosticsForDocument(doc.getText()) });
});

connection.onDidChangeTextDocument((evt) => {
  const doc = documents.get(evt.textDocument.uri);
  if (!doc) return;
  const nextText = evt.contentChanges.at(0)?.text ?? doc.getText();
  const next = TextDocument.create(doc.uri, doc.languageId, evt.textDocument.version, nextText);
  documents.set(next.uri, next);
  connection.sendDiagnostics({ uri: next.uri, diagnostics: diagnosticsForDocument(nextText) });
});

// ── Completions ─────────────────────────────────────────────────────────────

connection.onCompletion((params: TextDocumentPositionParams) => {
  const doc = documents.get(params.textDocument.uri);
  const line = doc
    ? doc.getText({ start: { line: params.position.line, character: 0 }, end: params.position })
    : "";

  // If we're after a key: provide VALUE completions
  const key = getKeyAtLine(line);
  if (key) {
    const values = VALUE_MAP.get(key);
    if (values) {
      return values.map((v) => ({
        label: v,
        kind: CompletionItemKind.EnumMember,
        detail: `${key} value`,
      }));
    }
  }

  // Otherwise: provide KEY completions
  return [
    ...JAML_ROOT_KEYS.map((k) => ({
      label: k,
      kind: CompletionItemKind.Property,
      detail: "JAML root key",
      insertText: `${k}: `,
    })),
    ...CLAUSE_KEYS.map((k) => ({
      label: k,
      kind: CompletionItemKind.Property,
      detail: "JAML clause key",
      insertText: `${k}: `,
    })),
  ];
});

// ── Hover ───────────────────────────────────────────────────────────────────

connection.onHover((params: TextDocumentPositionParams): Hover | null => {
  const doc = documents.get(params.textDocument.uri);
  if (!doc) return null;

  const lineText = doc.getText({
    start: { line: params.position.line, character: 0 },
    end: { line: params.position.line + 1, character: 0 },
  });
  const word = getWordAt(lineText, params.position.character);
  if (!word) return null;

  // Check which categories this word belongs to
  const categories: string[] = [];
  for (const [key, values] of VALUE_MAP) {
    if (values.includes(word)) {
      categories.push(key);
    }
  }

  if (categories.length > 0) {
    return {
      contents: {
        kind: MarkupKind.Markdown,
        value: `**${word}** — ${categories.join(", ")}`,
      },
    };
  }

  return null;
});

connection.listen();
