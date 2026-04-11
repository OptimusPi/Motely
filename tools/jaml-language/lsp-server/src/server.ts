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
} from "@motely/jaml-language-core";

// ── Schema-driven value data ────────────────────────────────────────────────

/** Map from JAML key → list of valid enum values, built from jaml.schema.json. */
const VALUE_MAP = new Map<string, string[]>();
const ROOT_KEY_SET = new Set<string>(JAML_ROOT_KEYS as readonly string[]);
const CLAUSE_KEY_SET = new Set<string>(CLAUSE_KEYS as readonly string[]);
const SOURCE_KEY_SET = new Set<string>();

function loadSchema(): any {
  // Schema lives next to the bundled server.js (esbuild copies it during build).
  // Fallback: repo root (dev mode).
  const here = __dirname;
  const candidates = [
    resolve(here, "jaml.schema.json"),
    resolve(here, "..", "jaml.schema.json"),
    resolve(here, "..", "..", "..", "jaml.schema.json"),
  ];

  for (const p of candidates) {
    try {
      return JSON.parse(readFileSync(p, "utf8"));
    } catch {}
  }

  return null;
}

function resolveSchemaRef(schema: any, ref: string | undefined): any {
  if (!ref || !ref.startsWith("#/")) return null;

  let current = schema;
  for (const segment of ref.slice(2).split("/")) {
    current = current?.[segment];
    if (current == null) return null;
  }

  return current;
}

function getSchemaNode(schema: any, definition: any): any {
  if (!definition || typeof definition !== "object") return null;
  return definition.$ref ? resolveSchemaRef(schema, definition.$ref) : definition;
}

function getSchemaProperties(schema: any, definition: any): Record<string, any> {
  const node = getSchemaNode(schema, definition);
  const properties = node?.properties;
  return properties && typeof properties === "object" ? properties : {};
}

function getSchemaEnumValues(schema: any, definition: any): string[] | null {
  const node = getSchemaNode(schema, definition);
  if (!node || typeof node !== "object") return null;

  if (Array.isArray(node.enum)) {
    return node.enum;
  }

  if (Array.isArray(node.items?.enum)) {
    return node.items.enum;
  }

  if (node.items) {
    return getSchemaEnumValues(schema, node.items);
  }

  return null;
}

function unknownKeys(object: Record<string, unknown>, allowed: Set<string>): string[] {
  return Object.keys(object).filter((key) => !allowed.has(key));
}

function pushDiagnostic(
  diagnostics: Diagnostic[],
  seenMessages: Set<string>,
  severity: DiagnosticSeverity,
  message: string,
  max: number
): void {
  if (seenMessages.has(message)) return;
  seenMessages.add(message);
  diagnostics.push({
    severity,
    range: { start: { line: 0, character: 0 }, end: { line: 0, character: max } },
    message,
    source: "jaml-lsp",
  });
}

function validateSourcesObject(
  sources: unknown,
  diagnostics: Diagnostic[],
  seenMessages: Set<string>,
  max: number
): void {
  if (!sources || typeof sources !== "object" || Array.isArray(sources)) {
    pushDiagnostic(diagnostics, seenMessages, DiagnosticSeverity.Warning, "Clause 'sources' must be an object/mapping.", max);
    return;
  }

  if (SOURCE_KEY_SET.size === 0) return;

  for (const bad of unknownKeys(sources as Record<string, unknown>, SOURCE_KEY_SET)) {
    pushDiagnostic(diagnostics, seenMessages, DiagnosticSeverity.Warning, `Unknown source key '${bad}'.`, max);
  }
}

function validateClauseObject(
  clause: unknown,
  diagnostics: Diagnostic[],
  seenMessages: Set<string>,
  max: number
): void {
  if (!clause || typeof clause !== "object" || Array.isArray(clause)) {
    pushDiagnostic(diagnostics, seenMessages, DiagnosticSeverity.Warning, "JAML clauses must be objects/mappings.", max);
    return;
  }

  const clauseObject = clause as Record<string, unknown>;
  for (const bad of unknownKeys(clauseObject, CLAUSE_KEY_SET)) {
    pushDiagnostic(diagnostics, seenMessages, DiagnosticSeverity.Warning, `Unknown clause key '${bad}'.`, max);
  }

  if ("sources" in clauseObject) {
    validateSourcesObject(clauseObject.sources, diagnostics, seenMessages, max);
  }

  for (const nestedKey of ["and", "or", "clauses"] as const) {
    validateClauseList(nestedKey, clauseObject[nestedKey], diagnostics, seenMessages, max);
  }
}

function validateClauseList(
  sectionName: string,
  clauses: unknown,
  diagnostics: Diagnostic[],
  seenMessages: Set<string>,
  max: number
): void {
  if (clauses == null) return;

  if (!Array.isArray(clauses)) {
    pushDiagnostic(diagnostics, seenMessages, DiagnosticSeverity.Warning, `JAML section '${sectionName}' must be an array of clauses.`, max);
    return;
  }

  for (const clause of clauses) {
    validateClauseObject(clause, diagnostics, seenMessages, max);
  }
}

function loadSchemaValues(): void {
  const schema = loadSchema();
  if (!schema) return;

  const props = getSchemaProperties(schema, schema);
  for (const [key, def] of Object.entries<any>(props)) {
    ROOT_KEY_SET.add(key);

    const values = getSchemaEnumValues(schema, def);
    if (values) {
      VALUE_MAP.set(key, values);
    }
  }

  const clauseDefs = getSchemaProperties(
    schema,
    props.must?.items ?? props.should?.items ?? props.mustNot?.items
  );
  for (const [key, def] of Object.entries<any>(clauseDefs)) {
    CLAUSE_KEY_SET.add(key);

    const values = getSchemaEnumValues(schema, def);
    if (values && !VALUE_MAP.has(key)) {
      VALUE_MAP.set(key, values);
    }
  }

  const sourceDefs = getSchemaProperties(schema, clauseDefs.sources);
  for (const key of Object.keys(sourceDefs)) {
    SOURCE_KEY_SET.add(key);
  }

  const aesthetics = getSchemaEnumValues(schema, props.aesthetics);
  if (aesthetics) {
    VALUE_MAP.set("aesthetics", aesthetics);
  }
}

loadSchemaValues();

// ── Helpers ─────────────────────────────────────────────────────────────────

const connection = createConnection(ProposedFeatures.all);
const documents = new Map<string, TextDocument>();

function normalizeEnumVariant(value: string): string {
  return value.replace(/[\s_\-'.]/g, "").toLowerCase();
}

function preferEnumVariant(current: string, candidate: string): string {
  // Prefer friendlier literals over legacy lowercase compact aliases.
  const rank = (v: string): number => {
    if (v === "Any") return 5;
    if (v.startsWith("Any") && /[A-Z]/.test(v.slice(1))) return 4;
    if (/^[A-Z][A-Za-z0-9]*$/.test(v)) return 3;
    if (v.includes(" ")) return 2;
    return 1;
  };
  return rank(candidate) > rank(current) ? candidate : current;
}

function dedupeEnumVariants(values: string[]): string[] {
  const byNormalized = new Map<string, string>();
  for (const value of values) {
    const key = normalizeEnumVariant(value);
    const existing = byNormalized.get(key);
    byNormalized.set(key, existing ? preferEnumVariant(existing, value) : value);
  }
  return Array.from(byNormalized.values());
}

/** Detect which JAML key the cursor is on: returns the key if the line is `key: <cursor>`. */
function getKeyAtLine(line: string): string | null {
  const m = line.match(/^\s*(?:-\s*)?(\w[\w-]*):\s*/);
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
  const seenMessages = new Set<string>();

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

    const rootObject = root as Record<string, unknown>;
    for (const bad of unknownKeys(rootObject, ROOT_KEY_SET)) {
      pushDiagnostic(diagnostics, seenMessages, DiagnosticSeverity.Warning, `Unknown root key '${bad}'.`, max);
    }

    for (const sectionName of ["must", "should", "mustNot"] as const) {
      validateClauseList(sectionName, rootObject[sectionName], diagnostics, seenMessages, max);
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
      return dedupeEnumVariants(values).map((v) => ({
        label: v,
        kind: CompletionItemKind.EnumMember,
        detail: `${key} value`,
      }));
    }
  }

  // Otherwise: provide KEY completions
  return [
    ...Array.from(ROOT_KEY_SET).map((k) => ({
      label: k,
      kind: CompletionItemKind.Property,
      detail: "JAML root key",
      insertText: `${k}: `,
    })),
    ...Array.from(CLAUSE_KEY_SET).map((k) => ({
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
