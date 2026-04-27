import * as vscode from 'vscode';
import { ensureMotely } from './motely.js';

export class JamlNotebookController {
  readonly id = 'jaml-kernel';
  readonly notebookType = 'jaml-notebook';
  readonly label = 'JAML Kernel (Motely)';

  private readonly _controller: vscode.NotebookController;
  private _executionOrder = 0;

  constructor() {
    this._controller = vscode.notebooks.createNotebookController(
      this.id,
      this.notebookType,
      this.label
    );
    this._controller.supportedLanguages = ['jaml'];
    this._controller.supportsExecutionOrder = true;
    this._controller.executeHandler = this._execute.bind(this);
  }

  dispose() {
    this._controller.dispose();
  }

  private async _execute(
    cells: vscode.NotebookCell[],
    _notebook: vscode.NotebookDocument,
    _controller: vscode.NotebookController
  ): Promise<void> {
    for (const cell of cells) {
      await this._doExecution(cell);
    }
  }

  private async _doExecution(cell: vscode.NotebookCell): Promise<void> {
    const execution = this._controller.createNotebookCellExecution(cell);
    execution.executionOrder = ++this._executionOrder;
    execution.start(Date.now());

    const jaml = cell.document.getText().trim();
    if (!jaml) {
      execution.replaceOutput([
        new vscode.NotebookCellOutput([
          vscode.NotebookCellOutputItem.text('Empty cell — write a JAML filter to validate.')
        ])
      ]);
      execution.end(true, Date.now());
      return;
    }

    try {
      const motely = await ensureMotely();
      const version = motely.MotelyWasm.getVersion();
      const result = motely.MotelyWasm.validateJamlStructured(jaml);

      if (!result.valid) {
        const location = result.line > 0
          ? ` (line ${result.line}, col ${result.column})`
          : '';
        execution.replaceOutput([
          new vscode.NotebookCellOutput([
            vscode.NotebookCellOutputItem.error({
              name: 'JAML Validation Error',
              message: `${result.message ?? 'Invalid JAML'}${location}`,
            })
          ])
        ]);
        execution.end(false, Date.now());
        return;
      }

      const meta = motely.MotelyWasm.getJamlMeta(jaml);
      const parts = [
        `Motely v${version}`,
        `Deck: ${meta.deck}  Stake: ${meta.stake}`,
        `Antes: [${Array.from(meta.antes).join(', ')}]`,
        `Clauses: ${meta.mustCount} must, ${meta.shouldCount} should, ${meta.mustNotCount} mustNot`,
      ];
      if (meta.itemTypes.length > 0) {
        parts.push(`Item types: ${meta.itemTypes.join(', ')}`);
      }
      parts.push('', 'Valid JAML filter ready to search.');

      execution.replaceOutput([
        new vscode.NotebookCellOutput([
          vscode.NotebookCellOutputItem.text(parts.join('\n'))
        ])
      ]);
      execution.end(true, Date.now());
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      execution.replaceOutput([
        new vscode.NotebookCellOutput([
          vscode.NotebookCellOutputItem.error({
            name: 'Motely Error',
            message,
          })
        ])
      ]);
      execution.end(false, Date.now());
    }
  }
}
