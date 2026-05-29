import {
  createConnection,
  TextDocuments,
  ProposedFeatures,
  TextDocumentSyncKind,
  CompletionItemKind,
  MarkupKind,
  SymbolKind,
  DiagnosticSeverity,
  type InitializeParams,
  type InitializeResult,
  type CompletionItem,
  type CompletionParams,
  type Hover,
  type HoverParams,
  type Diagnostic,
  type DocumentSymbol,
  type DocumentSymbolParams,
} from "vscode-languageserver/node";
import { TextDocument } from "vscode-languageserver-textdocument";
import * as jaml from "@motely/jaml-lang";
import { engineDiagnostics } from "./engine.js";

const connection = createConnection(ProposedFeatures.all);
const documents = new TextDocuments(TextDocument);

connection.onInitialize((_params: InitializeParams): InitializeResult => ({
  capabilities: {
    textDocumentSync: TextDocumentSyncKind.Incremental,
    completionProvider: { triggerCharacters: [":", " ", "-"] },
    hoverProvider: true,
    documentSymbolProvider: true,
  },
}));

const COMPLETION_KIND: Record<jaml.CompletionKind, CompletionItemKind> = {
  field: CompletionItemKind.Property,
  enum: CompletionItemKind.EnumMember,
  keyword: CompletionItemKind.Keyword,
  value: CompletionItemKind.Value,
};

const SYMBOL_KIND: Record<jaml.DocumentSymbol["kind"], SymbolKind> = {
  array: SymbolKind.Array,
  object: SymbolKind.Object,
  field: SymbolKind.Field,
};

function toLspDiagnostic(d: jaml.Diagnostic): Diagnostic {
  return {
    range: d.range,
    message: d.message,
    severity: d.severity as unknown as DiagnosticSeverity,
    source: d.source,
    code: d.code,
  };
}

// Validate on open/change: publish the fast structural layer immediately, then
// merge the authoritative engine result on top once it's ready (if available).
async function validate(document: TextDocument): Promise<void> {
  const text = document.getText();
  const fast = jaml.getDiagnostics(text);
  connection.sendDiagnostics({
    uri: document.uri,
    version: document.version,
    diagnostics: fast.map(toLspDiagnostic),
  });

  const engine = await engineDiagnostics(text);
  if (engine.length === 0) return;

  // Only re-publish if the document hasn't changed since we started.
  const latest = documents.get(document.uri);
  if (!latest || latest.version !== document.version) return;
  const merged = jaml.mergeDiagnostics(fast, engine);
  connection.sendDiagnostics({
    uri: document.uri,
    version: document.version,
    diagnostics: merged.map(toLspDiagnostic),
  });
}

documents.onDidChangeContent((e) => {
  void validate(e.document);
});

documents.onDidClose((e) => {
  connection.sendDiagnostics({ uri: e.document.uri, diagnostics: [] });
});

connection.onCompletion((params: CompletionParams): CompletionItem[] => {
  const doc = documents.get(params.textDocument.uri);
  if (!doc) return [];
  const offset = doc.offsetAt(params.position);
  return jaml.getCompletions(doc.getText(), offset).map((c) => ({
    label: c.label,
    kind: COMPLETION_KIND[c.kind],
    detail: c.detail,
    documentation: c.documentation
      ? { kind: MarkupKind.Markdown, value: c.documentation }
      : undefined,
    insertText: c.insertText,
  }));
});

connection.onHover((params: HoverParams): Hover | null => {
  const doc = documents.get(params.textDocument.uri);
  if (!doc) return null;
  const offset = doc.offsetAt(params.position);
  const h = jaml.getHover(doc.getText(), offset);
  if (!h) return null;
  return {
    contents: { kind: MarkupKind.Markdown, value: h.contents },
    range: h.range,
  };
});

connection.onDocumentSymbol((params: DocumentSymbolParams): DocumentSymbol[] => {
  const doc = documents.get(params.textDocument.uri);
  if (!doc) return [];
  const toLsp = (s: jaml.DocumentSymbol): DocumentSymbol => ({
    name: s.name,
    detail: s.detail,
    kind: SYMBOL_KIND[s.kind],
    range: s.range,
    selectionRange: s.selectionRange,
    children: s.children?.map(toLsp),
  });
  return jaml.getDocumentSymbols(doc.getText()).map(toLsp);
});

documents.listen(connection);
connection.listen();
