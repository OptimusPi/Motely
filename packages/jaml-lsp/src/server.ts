import bootsharp, { Jimmolate } from "motely-wasm";
import YAML from "yaml";

Jimmolate.filter = () => 1;
import {
  createConnection,
  type CompletionItem,
  type Definition,
  type DefinitionLink,
  type Diagnostic,
  type Hover,
  ProposedFeatures,
  TextDocumentSyncKind,
  TextDocuments,
} from "vscode-languageserver/node.js";
import { TextDocument } from "vscode-languageserver-textdocument";
import { findContext, getCompletions } from "./completion.js";
import { getDefinition } from "./definition.js";
import { getHover } from "./hover.js";
import { validateDocument } from "./validation.js";
import { loadVocabulary } from "./vocab.js";

const connection = createConnection(ProposedFeatures.all);
const documents = new TextDocuments(TextDocument);

await bootsharp.boot();

const vocabulary = loadVocabulary();

function sendDiagnostics(document: TextDocument): void {
  const diagnostics = validateDocument(document);
  connection.sendDiagnostics({ uri: document.uri, diagnostics });
}

documents.onDidChangeContent((change) => sendDiagnostics(change.document));
documents.onDidClose((change) => connection.sendDiagnostics({ uri: change.document.uri, diagnostics: [] }));

connection.onInitialize(() => ({
  capabilities: {
    textDocumentSync: TextDocumentSyncKind.Incremental,
    completionProvider: {
      triggerCharacters: [":", "-", " ", "."],
    },
    hoverProvider: true,
    definitionProvider: true,
    diagnosticProvider: {
      interFileDependencies: false,
      workspaceDiagnostics: false,
    },
  },
}));

connection.onCompletion((params): CompletionItem[] => {
  const document = documents.get(params.textDocument.uri);
  if (!document) return [];
  const offset = document.offsetAt(params.position);
  return getCompletions(document.getText(), offset, vocabulary);
});

connection.onHover((params): Hover | null => {
  const document = documents.get(params.textDocument.uri);
  if (!document) return null;
  const offset = document.offsetAt(params.position);
  return getHover(document.getText(), offset);
});

connection.onDefinition(async (params): Promise<Definition | DefinitionLink[] | null> => {
  const document = documents.get(params.textDocument.uri);
  if (!document) return null;
  const text = document.getText();
  const offset = document.offsetAt(params.position);

  let word = getWordAt(text, offset);

  // If the cursor is on a mapping key, use the full key name.
  try {
    const doc = YAML.parseDocument(text, { lineCounter: new YAML.LineCounter() });
    const contents = doc.contents as unknown;
    if (contents instanceof YAML.YAMLMap || contents instanceof YAML.YAMLSeq || contents instanceof YAML.Scalar) {
      const ctx = findContext(contents, offset);
      if (ctx?.inKey && ctx.node instanceof YAML.Scalar && typeof ctx.node.value === "string") {
        word = ctx.node.value;
      }
    }
  } catch {
    // ignore parse errors
  }

  return getDefinition(word);
});

function getWordAt(text: string, offset: number): string {
  const before = text.slice(0, offset);
  const after = text.slice(offset);
  const matchBefore = before.match(/[\s:\[\]{},'"]+$/);
  const matchAfter = after.match(/^[\s:\[\]{},'"]+/);
  const start = matchBefore ? before.length - matchBefore[0].length : 0;
  const end = matchAfter ? offset + matchAfter[0].length : offset;
  return text.slice(start, end).trim();
}

documents.listen(connection);
connection.listen();
