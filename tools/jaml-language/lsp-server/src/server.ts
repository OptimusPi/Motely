import {
  CompletionItemKind,
  createConnection,
  Diagnostic,
  DiagnosticSeverity,
  DidChangeConfigurationNotification,
  InitializeParams,
  InitializeResult,
  ProposedFeatures,
  TextDocumentSyncKind,
} from "vscode-languageserver/node.js";
import { TextDocument } from "vscode-languageserver-textdocument";
import { parse as parseYaml } from "yaml";
import {
  CLAUSE_KEYS,
  JAML_ROOT_KEYS,
  looksLikeJson,
  unknownRootKeys,
} from "@motely/jaml-language-core";

const connection = createConnection(ProposedFeatures.all);
const documents = new Map<string, TextDocument>();

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
        range: {
          start: { line: 0, character: 0 },
          end: { line: 0, character: max },
        },
        message: "JAML root must be an object/mapping.",
        source: "jaml-lsp",
      });
      return diagnostics;
    }

    for (const bad of unknownRootKeys(root as Record<string, unknown>)) {
      diagnostics.push({
        severity: DiagnosticSeverity.Warning,
        range: {
          start: { line: 0, character: 0 },
          end: { line: 0, character: max },
        },
        message: `Unknown root key '${bad}'.`,
        source: "jaml-lsp",
      });
    }
  } catch (error) {
    diagnostics.push({
      severity: DiagnosticSeverity.Error,
      range: {
        start: { line: 0, character: 0 },
        end: { line: 0, character: max },
      },
      message: `Parse error: ${(error as Error).message}`,
      source: "jaml-lsp",
    });
  }

  return diagnostics;
}

connection.onInitialize((_params: InitializeParams): InitializeResult => {
  return {
    capabilities: {
      textDocumentSync: TextDocumentSyncKind.Full,
      completionProvider: {
        resolveProvider: false,
      },
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

connection.onCompletion(() => {
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

connection.listen();