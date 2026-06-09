// JAML language server — LSP over stdio.
//
// This is a THIN adapter. All language intelligence lives in `jaml-lang`'s
// service (diagnostics / completions / hover / symbols), which is itself
// generated from the Motely C# engine enums. The server does ZERO language
// logic of its own — it only translates the service's already-LSP-shaped
// results onto the wire. One source of truth; no schema #4.

import {
  createConnection,
  TextDocuments,
  ProposedFeatures,
  InitializeParams,
  InitializeResult,
  TextDocumentSyncKind,
  CompletionItem,
  CompletionItemKind,
  Hover,
  DocumentSymbol,
  SymbolKind,
  Diagnostic,
  DiagnosticSeverity,
} from "vscode-languageserver/node.js";
import { TextDocument } from "vscode-languageserver-textdocument";

import {
  getDiagnostics,
  getCompletions,
  getHover,
  getDocumentSymbols,
  type CompletionKind,
  type DocumentSymbol as JamlSymbol,
} from "jaml-lang/service";

const connection = createConnection(ProposedFeatures.all);
const documents = new TextDocuments(TextDocument);

connection.onInitialize((_params: InitializeParams): InitializeResult => {
  return {
    capabilities: {
      textDocumentSync: TextDocumentSyncKind.Incremental,
      completionProvider: {
        // JAML keys/values key off `:` and whitespace; offer on those + manual.
        triggerCharacters: [":", " ", "-", "\n"],
        resolveProvider: false,
      },
      hoverProvider: true,
      documentSymbolProvider: true,
    },
    serverInfo: { name: "jaml-language-server", version: "0.1.0" },
  };
});

// ── Diagnostics: validate on open + change. ────────────────────────────────
function validate(doc: TextDocument): void {
  // service.Severity is already 1..4 == LSP DiagnosticSeverity 1..4, and ranges
  // are 0-based line/character — a direct structural match, no remapping.
  const diagnostics: Diagnostic[] = getDiagnostics(doc.getText()).map((d) => ({
    range: d.range,
    message: d.message,
    severity: d.severity as unknown as DiagnosticSeverity,
    source: d.source,
    code: d.code,
  }));
  connection.sendDiagnostics({ uri: doc.uri, diagnostics });
}

documents.onDidChangeContent((e) => validate(e.document));
documents.onDidOpen((e) => validate(e.document));

// ── Completion ─────────────────────────────────────────────────────────────
const COMPLETION_KIND: Record<CompletionKind, CompletionItemKind> = {
  keyword: CompletionItemKind.Keyword,
  enum: CompletionItemKind.EnumMember,
  field: CompletionItemKind.Field,
  value: CompletionItemKind.Value,
};

connection.onCompletion((params): CompletionItem[] => {
  const doc = documents.get(params.textDocument.uri);
  if (!doc) return [];
  const offset = doc.offsetAt(params.position);
  return getCompletions(doc.getText(), offset).map((c) => ({
    label: c.label,
    kind: COMPLETION_KIND[c.kind],
    detail: c.detail,
    documentation: c.documentation,
    insertText: c.insertText,
  }));
});

// ── Hover ──────────────────────────────────────────────────────────────────
connection.onHover((params): Hover | null => {
  const doc = documents.get(params.textDocument.uri);
  if (!doc) return null;
  const offset = doc.offsetAt(params.position);
  const h = getHover(doc.getText(), offset);
  if (!h) return null;
  return {
    contents: { kind: "markdown", value: h.contents },
    range: h.range,
  };
});

// ── Document symbols (must / should / mustNot outline) ──────────────────────
const SYMBOL_KIND: Record<JamlSymbol["kind"], SymbolKind> = {
  field: SymbolKind.Field,
  array: SymbolKind.Array,
  object: SymbolKind.Object,
};

function toLspSymbol(s: JamlSymbol): DocumentSymbol {
  return {
    name: s.name,
    detail: s.detail,
    kind: SYMBOL_KIND[s.kind],
    range: s.range,
    selectionRange: s.selectionRange,
    children: s.children?.map(toLspSymbol),
  };
}

connection.onDocumentSymbol((params): DocumentSymbol[] => {
  const doc = documents.get(params.textDocument.uri);
  if (!doc) return [];
  return getDocumentSymbols(doc.getText()).map(toLspSymbol);
});

documents.listen(connection);
connection.listen();
