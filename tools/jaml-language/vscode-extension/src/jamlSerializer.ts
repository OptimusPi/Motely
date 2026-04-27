import * as vscode from 'vscode';

interface RawJamlNotebook {
  cells: RawJamlCell[];
}

interface RawJamlCell {
  kind: 'jaml' | 'markdown';
  source: string;
  metadata?: Record<string, unknown>;
}

export class JamlNotebookSerializer implements vscode.NotebookSerializer {
  async deserializeNotebook(
    content: Uint8Array,
    _token: vscode.CancellationToken
  ): Promise<vscode.NotebookData> {
    const text = new TextDecoder().decode(content);

    let raw: RawJamlCell[];
    try {
      const parsed = JSON.parse(text) as RawJamlNotebook;
      raw = parsed.cells ?? [];
    } catch {
      raw = text.trim()
        ? [{ kind: 'jaml' as const, source: text }]
        : [];
    }

    const cells = raw.map(
      (cell) =>
        new vscode.NotebookCellData(
          cell.kind === 'markdown'
            ? vscode.NotebookCellKind.Markup
            : vscode.NotebookCellKind.Code,
          cell.source,
          cell.kind === 'markdown' ? 'markdown' : 'jaml'
        )
    );

    return new vscode.NotebookData(cells);
  }

  async serializeNotebook(
    data: vscode.NotebookData,
    _token: vscode.CancellationToken
  ): Promise<Uint8Array> {
    const cells: RawJamlCell[] = data.cells.map((cell) => ({
      kind: cell.kind === vscode.NotebookCellKind.Markup ? 'markdown' : 'jaml',
      source: cell.value,
    }));

    const notebook: RawJamlNotebook = { cells };
    return new TextEncoder().encode(JSON.stringify(notebook, null, 2));
  }
}
