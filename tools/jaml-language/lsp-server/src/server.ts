import {
  CompletionItemKind,
  createConnection,
  Diagnostic,
  DiagnosticSeverity,
  DidChangeConfigurationNotification,
  InitializeParams,
  InitializeResult,
  ProposedFeatures,
  TextDocumentPositionParams,
  TextDocumentSyncKind,
} from "vscode-languageserver/node.js";
import { TextDocument } from "vscode-languageserver-textdocument";
import { parse as parseYaml } from "yaml";
import { CLAUSE_KEYS, JAML_ROOT_KEYS, looksLikeJson } from "@motely/jaml-language-core";

const ROOT_KEY_SET = new Set<string>(JAML_ROOT_KEYS as readonly string[]);
const CLAUSE_KEY_SET = new Set<string>(CLAUSE_KEYS as readonly string[]);
/** Source keys under `sources:` — extend in core when you add known keys. */
const SOURCE_KEY_SET = new Set<string>();

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

/** Detect which JAML key the cursor is on: returns the key if the line is `key: <cursor>`. */
function getKeyAtLine(line: string): string | null {
  const m = line.match(/^\s*(?:-\s*)?(\w[\w-]*):\s*/);
  return m ? m[1] : null;
}

const connection = createConnection(ProposedFeatures.all);
const documents = new Map<string, TextDocument>();

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

// ── Completions (keys from core only; no JSON Schema enum lists) ────────────

connection.onCompletion((params: TextDocumentPositionParams) => {
  const doc = documents.get(params.textDocument.uri);
  const line = doc
    ? doc.getText({ start: { line: params.position.line, character: 0 }, end: params.position })
    : "";

  const key = getKeyAtLine(line);
  if (key) {
    return [];
  }

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

connection.listen();
