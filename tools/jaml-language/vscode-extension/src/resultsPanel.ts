import * as vscode from "vscode";
import type { SearchResult, SearchSummary } from "./searchRunner.js";

export class ResultsPanel {
  private static current: ResultsPanel | undefined;
  private readonly panel: vscode.WebviewPanel;
  private results: SearchResult[] = [];

  static getOrCreate(extensionUri: vscode.Uri): ResultsPanel {
    if (ResultsPanel.current) {
      ResultsPanel.current.panel.reveal();
      return ResultsPanel.current;
    }
    const panel = vscode.window.createWebviewPanel(
      "jamlResults",
      "JAML Search Results",
      vscode.ViewColumn.Beside,
      { enableScripts: true, retainContextWhenHidden: true }
    );
    ResultsPanel.current = new ResultsPanel(panel);
    return ResultsPanel.current;
  }

  private constructor(panel: vscode.WebviewPanel) {
    this.panel = panel;
    panel.onDidDispose(() => { ResultsPanel.current = undefined; });
    this.setHtml("idle");
  }

  searching(filter: string): void {
    this.results = [];
    this.setHtml("searching", filter);
  }

  addResult(seed: string, score: number): void {
    this.results.push({ seed, score });
    if (this.results.length % 10 === 0) this.flushTable();
  }

  done(summary: SearchSummary): void {
    this.results = summary.results;
    this.setHtml("done", undefined, summary);
  }

  error(message: string): void {
    this.setHtml("error", message);
  }

  private flushTable(): void {
    this.panel.webview.postMessage({ type: "results", results: this.results.slice(-10) });
  }

  private setHtml(state: "idle" | "searching" | "done" | "error", extra?: string, summary?: SearchSummary): void {
    this.panel.webview.html = buildHtml(state, extra, summary);
  }
}

function buildHtml(
  state: "idle" | "searching" | "done" | "error",
  extra?: string,
  summary?: SearchSummary
): string {
  const rows = summary?.results.slice(0, 500).map(r =>
    `<tr><td class="seed">${r.seed}</td><td class="score">${r.score > 0 ? r.score : "—"}</td></tr>`
  ).join("") ?? "";

  const header = {
    idle: "Waiting…",
    searching: `Searching… <span class="filter">${extra ?? ""}</span>`,
    done: `${summary?.matched} matches in ${summary?.searched} seeds · ${summary?.elapsedMs}ms · status: ${summary?.status}`,
    error: `Error: ${extra}`,
  }[state];

  return `<!DOCTYPE html>
<html>
<head>
<meta charset="UTF-8">
<style>
  body{font:13px/1.5 var(--vscode-editor-font-family,monospace);color:var(--vscode-editor-foreground);background:var(--vscode-editor-background);padding:1rem;margin:0}
  h2{font-size:1rem;margin:0 0 .75rem}
  .filter{opacity:.7;font-weight:normal}
  table{border-collapse:collapse;width:100%}
  th{text-align:left;padding:.25rem .5rem;border-bottom:1px solid var(--vscode-editorGroup-border);opacity:.6;font-weight:normal;font-size:.85rem}
  td{padding:.25rem .5rem}
  tr:hover{background:var(--vscode-list-hoverBackground)}
  .seed{font-weight:600;letter-spacing:.05em}
  .score{color:var(--vscode-charts-yellow);width:4rem}
  .spinner{display:inline-block;animation:spin 1s linear infinite;margin-right:.4rem}
  @keyframes spin{to{transform:rotate(360deg)}}
  .empty{opacity:.5;margin-top:1rem}
</style>
</head>
<body>
<h2>${state === "searching" ? '<span class="spinner">⟳</span>' : ""}${header}</h2>
${state === "done" && summary && summary.results.length > 0 ? `
<table>
  <thead><tr><th>Seed</th><th>Score</th></tr></thead>
  <tbody id="tbody">${rows}</tbody>
</table>` : state === "done" ? `<p class="empty">No matching seeds found.</p>` : ""}
${state === "searching" ? `<p class="empty" id="live">Running…</p>` : ""}
<script>
  const vscode = acquireVsCodeApi();
  const tbody = document.getElementById('tbody');
  const live = document.getElementById('live');
  window.addEventListener('message', e => {
    const {type, results} = e.data;
    if(type === 'results' && tbody && results){
      results.forEach(r => {
        const tr = document.createElement('tr');
        tr.innerHTML = '<td class="seed">'+r.seed+'</td><td class="score">'+(r.score>0?r.score:'—')+'</td>';
        tbody.appendChild(tr);
      });
      if(live) live.textContent = tbody.children.length + ' hits so far…';
    }
  });
</script>
</body>
</html>`;
}
