import * as vscode from "vscode";
import { runSearch } from "./searchRunner.js";

interface JamlCell {
  kind: "filter" | "markdown";
  source: string;
}

export class JamlNotebookSerializer implements vscode.NotebookSerializer {
  async deserializeNotebook(data: Uint8Array): Promise<vscode.NotebookData> {
    const text = new TextDecoder().decode(data);
    let cells: JamlCell[] = [];
    try {
      cells = JSON.parse(text);
    } catch {
      cells = [{ kind: "filter", source: text }];
    }
    return new vscode.NotebookData(
      cells.map(c => new vscode.NotebookCellData(
        c.kind === "markdown"
          ? vscode.NotebookCellKind.Markup
          : vscode.NotebookCellKind.Code,
        c.source,
        c.kind === "markdown" ? "markdown" : "jaml"
      ))
    );
  }

  async serializeNotebook(data: vscode.NotebookData): Promise<Uint8Array> {
    const cells: JamlCell[] = data.cells.map(c => ({
      kind: c.kind === vscode.NotebookCellKind.Markup ? "markdown" : "filter",
      source: c.value,
    }));
    return new TextEncoder().encode(JSON.stringify(cells, null, 2));
  }
}

export class JamlNotebookExecutor {
  private readonly extensionPath: string;

  constructor(extensionPath: string) {
    this.extensionPath = extensionPath;
  }

  register(context: vscode.ExtensionContext): vscode.Disposable {
    const controller = vscode.notebooks.createNotebookController(
      "jaml-kernel",
      "jaml-notebook",
      "JAML Seed Search",
    );
    controller.supportedLanguages = ["jaml", "jummy"];
    controller.description = "motely-wasm";
    controller.executeHandler = this.execute.bind(this);
    context.subscriptions.push(controller);
    return controller;
  }

  private execute(
    cells: vscode.NotebookCell[],
    _notebook: vscode.NotebookDocument,
    controller: vscode.NotebookController
  ): void {
    for (const cell of cells) {
      this.executeCell(cell, controller);
    }
  }

  private executeCell(cell: vscode.NotebookCell, controller: vscode.NotebookController): void {
    const execution = controller.createCellExecution(cell);
    execution.start(Date.now());
    execution.clearOutput();

    const jaml = cell.document.getText();
    const results: { seed: string; score: number }[] = [];
    let searched = 0n;

    runSearch(
      this.extensionPath,
      jaml,
      1_000_000,
      (s, _m) => { searched = s; },
      (seed, score) => { results.push({ seed, score }); },
      (summary) => {
        const sorted = summary.results;
        const tableHtml = buildNotebookHtml(sorted, summary);
        execution.appendOutput(new vscode.NotebookCellOutput([
          vscode.NotebookCellOutputItem.text(tableHtml, "text/html"),
          vscode.NotebookCellOutputItem.text(
            JSON.stringify(summary, null, 2), "application/json"
          ),
        ]));
        execution.end(true, Date.now());
      }
    ).catch(err => {
      execution.appendOutput(new vscode.NotebookCellOutput([
        vscode.NotebookCellOutputItem.error(err),
      ]));
      execution.end(false, Date.now());
    });
  }
}

function buildNotebookHtml(results: { seed: string; score: number }[], summary: { status: string; searched: string; matched: string; elapsedMs: number }): string {
  const rows = results.slice(0, 200).map(r =>
    `<tr><td style="font-weight:600;letter-spacing:.05em;padding:2px 8px">${r.seed}</td><td style="padding:2px 8px;opacity:.6">${r.score > 0 ? r.score : "—"}</td></tr>`
  ).join("");
  return `<div style="font:13px monospace">
<p style="margin:.25rem 0;opacity:.7">${summary.matched} matches · ${summary.searched} seeds · ${summary.elapsedMs}ms</p>
${results.length === 0 ? "<p>No matches.</p>" : `
<table style="border-collapse:collapse;width:100%">
  <thead><tr>
    <th style="text-align:left;padding:2px 8px;opacity:.5;font-weight:normal">Seed</th>
    <th style="text-align:left;padding:2px 8px;opacity:.5;font-weight:normal">Score</th>
  </tr></thead>
  <tbody>${rows}</tbody>
</table>`}
</div>`;
}
