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
  constructor() {}

  register(context: vscode.ExtensionContext): vscode.Disposable {
    const controller = vscode.notebooks.createNotebookController(
      "jaml-kernel",
      "jaml-notebook",
      "JAML Seed Search",
    );
    controller.supportedLanguages = ["jaml", "jummy"];
    controller.description = "motely-wasm-compat";
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
    const startTime = Date.now();
    execution.start(startTime);
    execution.clearOutput();

    const jaml = cell.document.getText();
    const results: { seed: string; score: number }[] = [];
    let searched = 0n;
    let matching = 0n;

    // Create a persistent output we'll update live as results stream in
    const liveOutput = new vscode.NotebookCellOutput([
      vscode.NotebookCellOutputItem.text(
        buildNotebookHtml([], { status: "running", searched: "0", matched: "0", elapsedMs: 0 }),
        "text/html"
      ),
    ]);
    execution.appendOutput(liveOutput);

    let lastRender = 0;
    const RENDER_INTERVAL_MS = 400;

    const renderLive = () => {
      const now = Date.now();
      if (now - lastRender < RENDER_INTERVAL_MS) return;
      lastRender = now;
      const sorted = results.slice().sort((a, b) => b.score - a.score).slice(0, 200);
      execution.replaceOutputItems(
        [vscode.NotebookCellOutputItem.text(
          buildNotebookHtml(sorted, { status: "running", searched: searched.toString(), matched: matching.toString(), elapsedMs: now - startTime }),
          "text/html"
        )],
        liveOutput
      );
    };

    runSearch(
      jaml,
      1_000_000,
      (s, m) => { searched = s; matching = m; renderLive(); },
      (seed, score) => { results.push({ seed, score }); },
      (summary) => {
        execution.replaceOutputItems(
          [
            vscode.NotebookCellOutputItem.text(buildNotebookHtml(summary.results, summary), "text/html"),
            vscode.NotebookCellOutputItem.text(JSON.stringify(summary, null, 2), "application/json"),
          ],
          liveOutput
        );
        execution.end(true, Date.now());
      }
    ).catch(err => {
      execution.replaceOutputItems(
        [vscode.NotebookCellOutputItem.error(err)],
        liveOutput
      );
      execution.end(false, Date.now());
    });
  }
}

function buildNotebookHtml(results: { seed: string; score: number }[], summary: { status: string; searched: string; matched: string; elapsedMs: number }): string {
  const isRunning = summary.status === "running";
  const rows = results.slice(0, 200).map(r =>
    `<tr><td style="font-weight:600;letter-spacing:.05em;padding:2px 8px">${r.seed}</td><td style="padding:2px 8px;opacity:.6">${r.score > 0 ? r.score : "—"}</td></tr>`
  ).join("");
  const searchedFmt = Number(BigInt(summary.searched)).toLocaleString();
  const statusLine = isRunning
    ? `<p style="margin:.25rem 0;opacity:.7">⏳ ${summary.matched} matches · ${searchedFmt} seeds · ${summary.elapsedMs}ms…</p>`
    : `<p style="margin:.25rem 0;opacity:.7">✓ ${summary.matched} matches · ${searchedFmt} seeds · ${summary.elapsedMs}ms</p>`;
  const tableHtml = results.length === 0
    ? (isRunning ? "" : "<p>No matches.</p>")
    : `<table style="border-collapse:collapse;width:100%">
  <thead><tr>
    <th style="text-align:left;padding:2px 8px;opacity:.5;font-weight:normal">Seed</th>
    <th style="text-align:left;padding:2px 8px;opacity:.5;font-weight:normal">Score</th>
  </tr></thead>
  <tbody>${rows}</tbody>
</table>`;
  return `<div style="font:13px monospace">\n${statusLine}\n${tableHtml}\n</div>`;
}
