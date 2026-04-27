import * as vscode from 'vscode';
import { ensureMotely } from './motely.js';

export function createJamlDiagnostics(context: vscode.ExtensionContext): void {
  const diagnostics = vscode.languages.createDiagnosticCollection('jaml');
  context.subscriptions.push(diagnostics);

  let debounceTimer: ReturnType<typeof setTimeout> | undefined;

  const validate = async (document: vscode.TextDocument) => {
    if (document.languageId !== 'jaml') return;

    const text = document.getText().trim();
    if (!text) {
      diagnostics.set(document.uri, []);
      return;
    }

    try {
      const motely = await ensureMotely();
      const result = motely.MotelyWasm.validateJamlStructured(text);

      if (result.valid) {
        diagnostics.set(document.uri, []);
        return;
      }

      const line = Math.max(0, result.line - 1);
      const col = Math.max(0, result.column - 1);
      const lineIdx = Math.min(line, document.lineCount - 1);
      const lineText = document.lineAt(lineIdx).text;
      const range = new vscode.Range(lineIdx, col, lineIdx, lineText.length);

      diagnostics.set(document.uri, [
        new vscode.Diagnostic(
          range,
          result.message ?? 'Invalid JAML',
          vscode.DiagnosticSeverity.Error
        ),
      ]);
    } catch {
      diagnostics.set(document.uri, []);
    }
  };

  context.subscriptions.push(
    vscode.workspace.onDidOpenTextDocument(validate),
    vscode.workspace.onDidSaveTextDocument(validate),
    vscode.workspace.onDidChangeTextDocument((e) => {
      if (e.document.languageId !== 'jaml') return;
      clearTimeout(debounceTimer);
      debounceTimer = setTimeout(() => validate(e.document), 300);
    }),
    vscode.workspace.onDidCloseTextDocument((doc) => {
      diagnostics.delete(doc.uri);
    })
  );

  for (const doc of vscode.workspace.textDocuments) {
    validate(doc);
  }
}
