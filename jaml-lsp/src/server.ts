import {
  createConnection,
  ProposedFeatures,
  TextDocuments,
  TextDocumentSyncKind,
  type InitializeResult,
  type CompletionItem as LspCompletionItem,
  type TextDocumentChangeEvent,
  type CompletionParams,
  type HoverParams,
  type Hover,
  CompletionItemKind,
  MarkupKind,
} from "vscode-languageserver/node";
import { TextDocument } from "vscode-languageserver-textdocument";
import { getDiagnostics, getCompletions, getHover } from "jaml-lang";

// Language server over stdio JSON-RPC, wrapping jaml-lang's validate/getCompletions/getHover so
// any LSP client (VS Code, Neovim, Zed, Claude Code's IDE diagnostics) gets JAML support.
// jaml-lang already emits LSP-shaped diagnostics, so this is transport only.

const connection = createConnection(ProposedFeatures.all);
const documents = new TextDocuments(TextDocument);

connection.onInitialize((): InitializeResult => ({
  capabilities: {
    textDocumentSync: TextDocumentSyncKind.Incremental,
    completionProvider: { triggerCharacters: [":", " ", "\n"] },
    hoverProvider: true,
  },
}));

function publish(doc: TextDocument): void {
  connection.sendDiagnostics({ uri: doc.uri, diagnostics: getDiagnostics(doc.getText()) });
}

documents.onDidChangeContent((e: TextDocumentChangeEvent<TextDocument>) => publish(e.document));
documents.onDidOpen((e: TextDocumentChangeEvent<TextDocument>) => publish(e.document));
documents.onDidClose((e: TextDocumentChangeEvent<TextDocument>) =>
  connection.sendDiagnostics({ uri: e.document.uri, diagnostics: [] }),
);

connection.onCompletion((params: CompletionParams): LspCompletionItem[] => {
  const doc = documents.get(params.textDocument.uri);
  if (!doc) return [];
  const offset = doc.offsetAt(params.position);
  return getCompletions(doc.getText(), offset).map((item) => ({
    label: item.label,
    kind:
      item.kind === "keyword" ? CompletionItemKind.Keyword
      : item.kind === "enum" ? CompletionItemKind.EnumMember
      : item.kind === "field" ? CompletionItemKind.Field
      : CompletionItemKind.Constant,
    detail: item.detail,
    documentation: item.documentation,
    insertText: item.insertText,
  }));
});

connection.onHover((params: HoverParams): Hover | null => {
  const doc = documents.get(params.textDocument.uri);
  if (!doc) return null;
  const info = getHover(doc.getText(), doc.offsetAt(params.position));
  if (!info) return null;
  return { contents: { kind: MarkupKind.Markdown, value: info.markdown } };
});

documents.listen(connection);
connection.listen();
