import * as vscode from 'vscode';
import { JamlNotebookSerializer } from './jamlSerializer.js';
import { JamlNotebookController } from './jamlController.js';
import { createJamlDiagnostics } from './jamlDiagnostics.js';
import { getMotelyStatusBarItem } from './motely.js';

export function activate(context: vscode.ExtensionContext) {
  context.subscriptions.push(
    vscode.workspace.registerNotebookSerializer(
      'jaml-notebook',
      new JamlNotebookSerializer(),
      { transientOutputs: true }
    )
  );
  context.subscriptions.push(new JamlNotebookController());
  context.subscriptions.push(getMotelyStatusBarItem());
  createJamlDiagnostics(context);
}

export function deactivate() {}
