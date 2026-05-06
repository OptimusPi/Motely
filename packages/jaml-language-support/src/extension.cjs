/**
 * JAML Language Support — VS Code Extension
 *
 * - Starts the jaml-lsp-server as a child process (LSP over Node IPC)
 * - Registers the Balatro Seed Curator MCP server into Copilot Chat
 * - Keeps syntax highlighting, snippets, and commands
 */

"use strict";

const vscode = require("vscode");
const fs = require("fs/promises");
const crypto = require("crypto");
const path = require("path");
const { pathToFileURL } = require("url");
const { LanguageClient, TransportKind } = require("vscode-languageclient/node");

const MCP_SERVER_ID = "balatro-seed-curator";
const MCP_SERVER_URL = "https://mcp.seedfinder.app/mcp";

let client;
let motelyWasmModulePromise;
let searchResultsPanel;

function activate(context) {
  startLspClient(context);
  registerMcpServer(context);
  registerCommands(context);
  registerChatParticipant(context);
  registerSeedAnalyzer(context);
  registerSidebar(context);
}

function deactivate() {
  return client?.stop();
}

// ---------------------------------------------------------------------------
// LSP client
// ---------------------------------------------------------------------------
function startLspClient(context) {
  const serverModule = context.asAbsolutePath(
    path.join("vendor", "jaml-lsp-server", "out", "server.js")
  );

  const serverOptions = {
    run: { module: serverModule, transport: TransportKind.ipc },
    debug: {
      module: serverModule,
      transport: TransportKind.ipc,
      options: { execArgv: ["--nolazy", "--inspect=6099"] },
    },
  };

  const clientOptions = {
    documentSelector: [
      { scheme: "file", language: "jaml" },
      { scheme: "untitled", language: "jaml" },
    ],
    synchronize: {
      fileEvents: vscode.workspace.createFileSystemWatcher("**/*.jaml"),
    },
  };

  client = new LanguageClient(
    "jaml-language-server",
    "JAML Language Server",
    serverOptions,
    clientOptions
  );

  client.start();
  context.subscriptions.push(client);
}

// ---------------------------------------------------------------------------
// MCP server registration (VS Code 1.99+)
// Registers Balatro Seed Curator so Copilot Chat gets it for free.
// ---------------------------------------------------------------------------
function registerMcpServer(context) {
  if (!vscode.lm?.registerMcpServerDefinitionProvider) {
    return; // older VS Code — skip silently
  }

  const provider = {
    provideMcpServerDefinitions() {
      return [
        new vscode.McpHttpServerDefinition(
          "Balatro Seed Curator",
          vscode.Uri.parse(MCP_SERVER_URL)
        ),
      ];
    },
  };

  context.subscriptions.push(
    vscode.lm.registerMcpServerDefinitionProvider(MCP_SERVER_ID, provider)
  );
}

// ---------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------
function registerCommands(context) {
  context.subscriptions.push(
    vscode.commands.registerCommand("jaml.openInCurator", () => {
      const editor = vscode.window.activeTextEditor;
      if (!editor || editor.document.languageId !== "jaml") {
        vscode.window.showInformationMessage("Open a .jaml file first.");
        return;
      }
      const encoded = encodeURIComponent(editor.document.getText());
      vscode.env.openExternal(
        vscode.Uri.parse(`https://jammy.seedfinder.app/?jaml=${encoded}`)
      );
    })
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("jaml.openSchema", () => {
      const schemaUri = vscode.Uri.joinPath(
        context.extensionUri,
        "vendor",
        "jaml-lsp-server",
        "node_modules",
        "motely-wasm",
        "jaml.schema.json"
      );
      vscode.workspace
        .openTextDocument(schemaUri)
        .then((doc) => vscode.window.showTextDocument(doc))
        .then(undefined, (err) => {
          vscode.window.showErrorMessage(
            `Could not open bundled schema: ${err?.message ?? err}\nExpected: ${schemaUri.fsPath}`
          );
        });
    })
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("jaml.searchCurrentFilter", async () => {
      await runCurrentFilterSearch(context, { openPanel: true });
    })
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("jaml.showDocumentSummary", () => {
      const editor = vscode.window.activeTextEditor;
      if (!editor || editor.document.languageId !== "jaml") {
        vscode.window.showInformationMessage("Open a .jaml file first.");
        return;
      }
      const text = editor.document.getText();
      const must = countItems(text, "must");
      const should = countItems(text, "should");
      const mustNot = countItems(text, "mustNot");
      const diags = vscode.languages.getDiagnostics(editor.document.uri).length;
      vscode.window.showInformationMessage(
        `JAML: must ${must}  should ${should}  mustNot ${mustNot}  diagnostics ${diags}`
      );
    })
  );
}

function countItems(text, section) {
  const pattern = new RegExp(
    `^${section}:\\s*\\r?\\n([\\s\\S]*?)(?=^[A-Za-z][A-Za-z0-9]*:\\s|$)`,
    "m"
  );
  const match = text.match(pattern);
  if (!match) return 0;
  return (match[1].match(/^\s*-\s+/gm) || []).length;
}

function normalizeSeedInput(value) {
  const seed = value.trim().toUpperCase();
  if (!/^[A-Z0-9]{1,8}$/.test(seed)) return null;
  return seed.replaceAll("0", "O");
}

function clampInt(value, min, max) {
  const num = Number(value);
  if (!Number.isFinite(num)) return min;
  return Math.min(max, Math.max(min, Math.trunc(num)));
}

function getWorkspaceRootPath() {
  return vscode.workspace.workspaceFolders?.[0]?.uri.fsPath ?? null;
}

function getSearchConfiguration() {
  const cfg = vscode.workspace.getConfiguration("jaml");
  const resultsPath = String(cfg.get("search.resultsPath", "") ?? "").trim();
  return {
    batchSize: clampInt(cfg.get("search.batchSize", 6), 1, 7),
    quickBatchCount: clampInt(cfg.get("search.quickBatchCount", 25), 1, 1000000),
    maxResults: clampInt(cfg.get("search.maxResults", 100), 1, 1000),
    resultsPath,
  };
}

function extractTopLevelScalar(jaml, key) {
  const match = jaml.match(new RegExp(`^${key}:\\s*(.+?)\\s*$`, "m"));
  if (!match) return null;
  const raw = match[1].trim();
  return raw.replace(/^['\"]|['\"]$/g, "").trim() || null;
}

function normalizeFilterIdSource(source) {
  if (!source || !source.trim()) return "unnamed";
  let normalized = source.trim().replace(/[^A-Za-z0-9_-]+/g, "-");
  normalized = normalized.replace(/-+/g, "-").replace(/^[-_]+|[-_]+$/g, "");
  return normalized ? normalized.toLowerCase() : "unnamed";
}

function getDocumentLabel(editor) {
  const fileName = editor?.document?.fileName || editor?.document?.uri?.fsPath || "Untitled Filter";
  return path.basename(fileName, ".jaml");
}

function getFilterIdentity(jaml, fallbackName) {
  const explicitId = extractTopLevelScalar(jaml, "id");
  const name = extractTopLevelScalar(jaml, "name") ?? fallbackName ?? null;
  const filterId = normalizeFilterIdSource(explicitId && explicitId.trim() ? explicitId : name);
  return { filterId, explicitId, name };
}

function formatCount(value) {
  const num = typeof value === "string" ? Number(value) : Number(value ?? 0);
  return Number.isFinite(num) ? num.toLocaleString() : String(value ?? "0");
}

function serializeCount(value) {
  return typeof value === "bigint" ? value.toString() : String(value ?? 0);
}

function hashJaml(jaml) {
  return crypto.createHash("sha256").update(jaml, "utf8").digest("hex");
}

function escapeHtml(value) {
  return String(value ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/\"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

function normalizeResultRows(results) {
  if (!Array.isArray(results)) return [];
  return results.map((hit) => ({
    seed: String(hit?.seed ?? ""),
    score: Number(hit?.score ?? 0),
    tallyColumns: Array.from(hit?.tallyColumns ?? [], (value) => Number(value)),
  }));
}

function escapeCsvValue(value) {
  const text = String(value ?? "");
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

function buildResultsCsv(results, tallyColumnCount) {
  const header = [
    "Seed",
    "Score",
    ...Array.from({ length: tallyColumnCount }, (_, index) => `tally${index + 1}`),
  ];
  const rows = results.map((row) => [
    row.seed,
    row.score,
    ...Array.from({ length: tallyColumnCount }, (_, index) => row.tallyColumns[index] ?? ""),
  ]);
  return [header, ...rows]
    .map((columns) => columns.map(escapeCsvValue).join(","))
    .join("\n");
}

async function resolveResultsPath(context) {
  const cfg = getSearchConfiguration();
  let resultsPath = cfg.resultsPath;
  if (!resultsPath) {
    resultsPath = path.join(context.globalStorageUri.fsPath, "results");
  } else if (!path.isAbsolute(resultsPath)) {
    const workspaceRoot = getWorkspaceRootPath();
    resultsPath = workspaceRoot
      ? path.join(workspaceRoot, resultsPath)
      : path.join(context.globalStorageUri.fsPath, resultsPath);
  }
  await fs.mkdir(resultsPath, { recursive: true });
  return resultsPath;
}

async function persistSearchRun(context, record) {
  const resultsRoot = await resolveResultsPath(context);
  const filterDir = path.join(resultsRoot, record.filterId);
  await fs.mkdir(filterDir, { recursive: true });

  const tallyColumnCount = Math.max(
    record.tallyLabels.length,
    ...record.results.map((row) => row.tallyColumns.length),
    0
  );

  const metadata = {
    filterId: record.filterId,
    explicitId: record.explicitId,
    name: record.name,
    deck: record.deck,
    stake: record.stake,
    jamlHash: record.jamlHash,
    sourceFile: record.sourceFile,
    batchSize: record.batchSize,
    quickBatchCount: record.quickBatchCount,
    maxResults: record.maxResults,
    startedAt: record.startedAt,
    finishedAt: record.finishedAt,
    state: record.state,
    error: record.error,
    totalSeedsSearched: record.totalSeedsSearched,
    matchingSeeds: record.matchingSeeds,
    displayedResults: record.results.length,
    tallyColumnCount,
    tallyLabels: record.tallyLabels,
  };

  const csv = buildResultsCsv(record.results, tallyColumnCount);

  await Promise.all([
    fs.writeFile(path.join(filterDir, "metadata.json"), JSON.stringify(metadata, null, 2), "utf8"),
    fs.writeFile(path.join(filterDir, "filter.jaml"), record.jaml, "utf8"),
    fs.writeFile(path.join(filterDir, "results.csv"), csv ? `${csv}\n` : "", "utf8"),
  ]);

  return {
    resultsRoot,
    filterDir,
    metadataFile: path.join(filterDir, "metadata.json"),
    resultsFile: path.join(filterDir, "results.csv"),
  };
}

function ensureSearchResultsPanel(context) {
  if (searchResultsPanel) {
    searchResultsPanel.reveal(vscode.ViewColumn.Beside);
    return searchResultsPanel;
  }

  searchResultsPanel = vscode.window.createWebviewPanel(
    "jaml.searchResults",
    "JAML Search Results",
    vscode.ViewColumn.Beside,
    { enableScripts: true, retainContextWhenHidden: true }
  );

  searchResultsPanel.onDidDispose(() => {
    searchResultsPanel = undefined;
  }, null, context.subscriptions);

  searchResultsPanel.webview.onDidReceiveMessage(async (msg) => {
    switch (msg.type) {
      case "rerun":
        await vscode.commands.executeCommand("jaml.searchCurrentFilter");
        break;
      case "openCurator":
        if (typeof msg.jaml === "string") {
          vscode.env.openExternal(
            vscode.Uri.parse(`https://jammy.seedfinder.app/?jaml=${encodeURIComponent(msg.jaml)}`)
          );
        }
        break;
      case "openResultsFolder":
        if (typeof msg.path === "string" && msg.path) {
          await vscode.commands.executeCommand("revealFileInOS", vscode.Uri.file(msg.path));
        }
        break;
      case "openSeed":
        if (typeof msg.seed === "string" && msg.seed) {
          vscode.env.openExternal(
            vscode.Uri.parse(`https://jammy.seedfinder.app/?seed=${encodeURIComponent(msg.seed)}`)
          );
        }
        break;
      case "copySeed":
        if (typeof msg.seed === "string" && msg.seed) {
          await vscode.env.clipboard.writeText(msg.seed);
          vscode.window.showInformationMessage(`Copied ${msg.seed}`);
        }
        break;
    }
  }, null, context.subscriptions);

  return searchResultsPanel;
}

function renderSearchResultsPanel(context, model) {
  const panel = ensureSearchResultsPanel(context);
  panel.title = model?.filterId ? `JAML Search: ${model.filterId}` : "JAML Search Results";
  panel.webview.html = getSearchResultsHtml(model);
}

function getSearchResultsHtml(model) {
  const results = Array.isArray(model?.results) ? model.results : [];
  const tallyLabels = Array.isArray(model?.tallyLabels) ? model.tallyLabels : [];
  const resultHeader = tallyLabels.length
    ? `<th>Seed</th><th>Score</th>${tallyLabels.map((label) => `<th>${escapeHtml(label)}</th>`).join("")}<th></th>`
    : "<th>Seed</th><th>Score</th><th></th>";
  const resultRows = results.length
    ? results.map((row) => {
      const tallies = tallyLabels.length
        ? row.tallyColumns.map((value) => `<td>${escapeHtml(value)}</td>`).join("")
        : "";
      return `<tr>
          <td><code>${escapeHtml(row.seed)}</code></td>
          <td>${escapeHtml(row.score)}</td>
          ${tallies}
          <td class="actions-cell">
            <button class="ghost" onclick="send('copySeed',{seed:${JSON.stringify(row.seed)}})">Copy</button>
            <button class="ghost" onclick="send('openSeed',{seed:${JSON.stringify(row.seed)}})">Curator</button>
          </td>
        </tr>`;
    }).join("")
    : `<tr><td colspan="${Math.max(3, tallyLabels.length + 3)}" class="empty-row">No displayed results for this run.</td></tr>`;
  const startedAt = model?.startedAt ? new Date(model.startedAt).toLocaleString() : "";
  const finishedAt = model?.finishedAt ? new Date(model.finishedAt).toLocaleString() : "";
  const filterTitle = model?.name || model?.filterId || "Current filter";
  const jaml = escapeHtml(model?.jaml || "");
  const running = model?.status === "running";
  const error = model?.status === "error" ? escapeHtml(model?.error || "Unknown error") : "";

  return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline';">
<style>
  :root {
    --bg: var(--vscode-editor-background, #1e1e1e);
    --panel: var(--vscode-sideBar-background, #252526);
    --panel-2: var(--vscode-editorWidget-background, #202020);
    --fg: var(--vscode-foreground, #d4d4d4);
    --muted: var(--vscode-descriptionForeground, #9da2b0);
    --border: var(--vscode-panel-border, #3c3c3c);
    --accent: #ffb347;
    --accent-2: #4fc3f7;
    --danger: #fe5f55;
  }
  * { box-sizing: border-box; }
  body {
    margin: 0;
    background: var(--bg);
    color: var(--fg);
    font-family: var(--vscode-font-family, sans-serif);
  }
  .shell {
    display: grid;
    grid-template-rows: auto auto 1fr;
    min-height: 100vh;
  }
  .topbar {
    display: flex;
    justify-content: space-between;
    gap: 12px;
    padding: 14px 16px 10px;
    border-bottom: 1px solid var(--border);
    background: var(--panel);
  }
  .title {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }
  .title h1 {
    margin: 0;
    font-size: 18px;
    font-weight: 600;
  }
  .meta {
    color: var(--muted);
    font-size: 12px;
    display: flex;
    gap: 12px;
    flex-wrap: wrap;
  }
  .actions {
    display: flex;
    gap: 8px;
    align-items: start;
    flex-wrap: wrap;
  }
  button {
    border: none;
    border-radius: 8px;
    padding: 8px 12px;
    cursor: pointer;
    font: inherit;
  }
  .primary { background: var(--accent); color: #1a1a1a; }
  .secondary { background: var(--accent-2); color: #1a1a1a; }
  .ghost {
    background: transparent;
    color: var(--fg);
    border: 1px solid var(--border);
  }
  .status {
    padding: 10px 16px;
    border-bottom: 1px solid var(--border);
    background: ${running ? "rgba(79,195,247,0.12)" : model?.status === "error" ? "rgba(254,95,85,0.12)" : "rgba(255,179,71,0.10)"};
    color: ${running ? "var(--accent-2)" : model?.status === "error" ? "var(--danger)" : "var(--fg)"};
  }
  .content {
    display: grid;
    grid-template-columns: minmax(280px, 36%) minmax(0, 1fr);
    min-height: 0;
  }
  .pane {
    min-height: calc(100vh - 126px);
    padding: 14px 16px;
  }
  .pane + .pane {
    border-left: 1px solid var(--border);
  }
  .card {
    background: var(--panel-2);
    border: 1px solid var(--border);
    border-radius: 10px;
    padding: 12px;
    margin-bottom: 12px;
  }
  .card h2 {
    margin: 0 0 8px;
    font-size: 14px;
    font-weight: 600;
  }
  .facts {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
    gap: 8px;
  }
  .fact {
    border: 1px solid var(--border);
    border-radius: 8px;
    padding: 8px 10px;
    background: rgba(255,255,255,0.02);
  }
  .fact .label {
    display: block;
    color: var(--muted);
    font-size: 11px;
    margin-bottom: 4px;
  }
  .fact .value {
    font-size: 13px;
    word-break: break-word;
  }
  pre {
    margin: 0;
    white-space: pre-wrap;
    word-break: break-word;
    line-height: 1.5;
    font-family: var(--vscode-editor-font-family, monospace);
    font-size: 12px;
  }
  table {
    width: 100%;
    border-collapse: collapse;
    font-size: 12px;
  }
  th, td {
    text-align: left;
    padding: 8px 10px;
    border-bottom: 1px solid var(--border);
    vertical-align: top;
  }
  th {
    position: sticky;
    top: 0;
    background: var(--panel-2);
    z-index: 1;
  }
  .table-wrap {
    border: 1px solid var(--border);
    border-radius: 10px;
    overflow: auto;
    max-height: calc(100vh - 260px);
    background: var(--panel-2);
  }
  .actions-cell {
    display: flex;
    gap: 6px;
    flex-wrap: wrap;
  }
  .empty-row {
    color: var(--muted);
    text-align: center;
    padding: 18px;
  }
  @media (max-width: 980px) {
    .content {
      grid-template-columns: 1fr;
    }
    .pane + .pane {
      border-left: none;
      border-top: 1px solid var(--border);
    }
  }
</style>
</head>
<body>
<div class="shell">
  <div class="topbar">
    <div class="title">
      <h1>${escapeHtml(filterTitle)}</h1>
      <div class="meta">
        <span>filterId: <code>${escapeHtml(model?.filterId || "unnamed")}</code></span>
        <span>deck: ${escapeHtml(model?.deck || "Red")}</span>
        <span>stake: ${escapeHtml(model?.stake || "White")}</span>
      </div>
    </div>
    <div class="actions">
      <button class="primary" onclick="send('rerun')">Run again</button>
      <button class="secondary" onclick="send('openCurator',{jaml:${JSON.stringify(model?.jaml || "")}})">Open in curator</button>
      <button class="ghost" onclick="send('openResultsFolder',{path:${JSON.stringify(model?.resultsRoot || "")}})">Open results folder</button>
    </div>
  </div>
  <div class="status">${running ? "Searching with motely-wasm and writing CSV results for this filter…" : model?.status === "error" ? error : `Stored latest CSV results for this filter in ${escapeHtml(model?.filterDir || model?.resultsRoot || "")}`}</div>
  <div class="content">
    <section class="pane">
      <div class="card">
        <h2>Run details</h2>
        <div class="facts">
          <div class="fact"><span class="label">Displayed results</span><span class="value">${formatCount(results.length)}</span></div>
          <div class="fact"><span class="label">Seeds searched</span><span class="value">${formatCount(model?.totalSeedsSearched || 0)}</span></div>
          <div class="fact"><span class="label">Matching seeds</span><span class="value">${formatCount(model?.matchingSeeds || 0)}</span></div>
          <div class="fact"><span class="label">Batch char count</span><span class="value">${escapeHtml(model?.batchSize || "")}</span></div>
          <div class="fact"><span class="label">Batch count scanned</span><span class="value">${escapeHtml(model?.quickBatchCount || "")}</span></div>
          <div class="fact"><span class="label">Result cap</span><span class="value">${escapeHtml(model?.maxResults || "")}</span></div>
          <div class="fact"><span class="label">Started</span><span class="value">${escapeHtml(startedAt)}</span></div>
          <div class="fact"><span class="label">Finished</span><span class="value">${escapeHtml(finishedAt)}</span></div>
          <div class="fact"><span class="label">Results root</span><span class="value">${escapeHtml(model?.resultsRoot || "")}</span></div>
          <div class="fact"><span class="label">Filter folder</span><span class="value">${escapeHtml(model?.filterDir || "")}</span></div>
          <div class="fact"><span class="label">CSV file</span><span class="value">${escapeHtml(model?.resultsFile || "")}</span></div>
        </div>
      </div>
      <div class="card">
        <h2>Current JAML</h2>
        <pre>${jaml}</pre>
      </div>
    </section>
    <section class="pane">
      <div class="card">
        <h2>Search results</h2>
        <div class="table-wrap">
          <table>
            <thead>
              <tr>${resultHeader}</tr>
            </thead>
            <tbody>
              ${resultRows}
            </tbody>
          </table>
        </div>
      </div>
    </section>
  </div>
</div>
<script>
const vscode = acquireVsCodeApi();
function send(type, extra) { vscode.postMessage({ type, ...extra }); }
</script>
</body>
</html>`;
}

async function runCurrentFilterSearch(context, options = {}) {
  const editor = vscode.window.activeTextEditor;
  if (!editor || editor.document.languageId !== "jaml") {
    const message = "Open a .jaml file first.";
    if (options.stream) options.stream.markdown(message);
    else vscode.window.showWarningMessage(message);
    return null;
  }

  const jaml = editor.document.getText();
  const label = getDocumentLabel(editor);
  const identity = getFilterIdentity(jaml, label);
  const cfg = getSearchConfiguration();

  renderSearchResultsPanel(context, {
    status: "running",
    filterId: identity.filterId,
    explicitId: identity.explicitId,
    name: identity.name ?? label,
    deck: extractTopLevelScalar(jaml, "deck") ?? "Red",
    stake: extractTopLevelScalar(jaml, "stake") ?? "White",
    jaml,
    batchSize: cfg.batchSize,
    quickBatchCount: cfg.quickBatchCount,
    maxResults: cfg.maxResults,
    results: [],
    tallyLabels: [],
    startedAt: new Date().toISOString(),
    finishedAt: null,
    totalSeedsSearched: 0,
    matchingSeeds: 0,
    resultsRoot: await resolveResultsPath(context),
    filterDir: "",
    resultsFile: "",
  });

  try {
    const motelyMod = await loadVendoredMotelyWasm(context);
    if (typeof motelyMod.default?.boot === "function") await motelyMod.default.boot();
    else if (typeof motelyMod.default?.initialize === "function") await motelyMod.default.initialize();
    const motely = motelyMod.MotelyWasm ?? motelyMod.default ?? motelyMod;

    const startedAt = new Date().toISOString();
    const tallyLabels = Array.from(motely.getTallyLabels(jaml) ?? []);
    const meta = typeof motely.getJamlMeta === "function" ? motely.getJamlMeta(jaml) : null;
    const batchResult = await motely.runSequentialSearchBatch(
      jaml,
      cfg.batchSize,
      0n,
      BigInt(cfg.quickBatchCount),
      cfg.maxResults
    );
    const finishedAt = new Date().toISOString();
    const results = normalizeResultRows(batchResult?.results);
    const persisted = await persistSearchRun(context, {
      filterId: identity.filterId,
      explicitId: identity.explicitId,
      name: identity.name ?? label,
      jaml,
      jamlHash: hashJaml(jaml),
      sourceFile: editor.document.uri.scheme === "file" ? editor.document.uri.fsPath : null,
      deck: meta?.deck ?? extractTopLevelScalar(jaml, "deck") ?? "Red",
      stake: meta?.stake ?? extractTopLevelScalar(jaml, "stake") ?? "White",
      batchSize: cfg.batchSize,
      quickBatchCount: cfg.quickBatchCount,
      maxResults: cfg.maxResults,
      startedAt,
      finishedAt,
      state: String(batchResult?.completion?.state ?? "Completed"),
      error: batchResult?.completion?.error ?? null,
      totalSeedsSearched: serializeCount(batchResult?.completion?.totalSeedsSearched),
      matchingSeeds: serializeCount(batchResult?.completion?.matchingSeeds),
      tallyLabels,
      results,
    });

    const model = {
      status: "complete",
      filterId: identity.filterId,
      explicitId: identity.explicitId,
      name: identity.name ?? label,
      deck: meta?.deck ?? extractTopLevelScalar(jaml, "deck") ?? "Red",
      stake: meta?.stake ?? extractTopLevelScalar(jaml, "stake") ?? "White",
      jaml,
      batchSize: cfg.batchSize,
      quickBatchCount: cfg.quickBatchCount,
      maxResults: cfg.maxResults,
      results,
      tallyLabels,
      startedAt,
      finishedAt,
      totalSeedsSearched: serializeCount(batchResult?.completion?.totalSeedsSearched),
      matchingSeeds: serializeCount(batchResult?.completion?.matchingSeeds),
      resultsRoot: persisted.resultsRoot,
      filterDir: persisted.filterDir,
      resultsFile: persisted.resultsFile,
    };

    if (options.openPanel !== false) {
      renderSearchResultsPanel(context, model);
    }

    if (options.stream) {
      options.stream.markdown(
        `Opened the search results panel for \`${identity.filterId}\`. Searched **${formatCount(model.totalSeedsSearched)}** seeds with \`motely-wasm\`, captured **${formatCount(model.matchingSeeds)}** matches, displayed **${formatCount(results.length)}** rows, and stored the latest run in \`${persisted.filterDir}\`.`
      );
    }

    return model;
  } catch (err) {
    const message = err?.message ?? String(err);
    renderSearchResultsPanel(context, {
      status: "error",
      filterId: identity.filterId,
      explicitId: identity.explicitId,
      name: identity.name ?? label,
      deck: extractTopLevelScalar(jaml, "deck") ?? "Red",
      stake: extractTopLevelScalar(jaml, "stake") ?? "White",
      jaml,
      batchSize: cfg.batchSize,
      quickBatchCount: cfg.quickBatchCount,
      maxResults: cfg.maxResults,
      results: [],
      tallyLabels: [],
      startedAt: new Date().toISOString(),
      finishedAt: new Date().toISOString(),
      totalSeedsSearched: 0,
      matchingSeeds: 0,
      resultsRoot: await resolveResultsPath(context),
      filterDir: "",
      resultsFile: "",
      error: message,
    });
    if (options.stream) options.stream.markdown(`Error running search: ${message}`);
    else vscode.window.showErrorMessage(`Error running search: ${message}`);
    return null;
  }
}

// ---------------------------------------------------------------------------
// Chat participant — @jimbo
// VS Code 1.97+ Language Model API
// ---------------------------------------------------------------------------
function registerChatParticipant(context) {
  if (!vscode.chat?.createChatParticipant) return; // VS Code < 1.97

  const participant = vscode.chat.createChatParticipant(
    "jaml.jimbo",
    (request, chatContext, stream, token) => handleJimboChat(context, request, chatContext, stream, token)
  );
  participant.iconPath = vscode.Uri.joinPath(
    context.extensionUri,
    "images",
    "icon.ico"
  );
  context.subscriptions.push(participant);
}

async function loadVendoredMotelyWasm(context) {
  motelyWasmModulePromise ??= import(
    pathToFileURL(
      context.asAbsolutePath(
        path.join("vendor", "jaml-lsp-server", "node_modules", "motely-wasm", "index.mjs")
      )
    ).href
  );

  return motelyWasmModulePromise;
}

async function handleJimboChat(extensionContext, request, _context, stream, token) {
  const editor = vscode.window.activeTextEditor;
  const isJaml = editor?.document.languageId === "jaml";

  let userPrompt = request.prompt;

  if (request.command === "explain") {
    if (!isJaml) {
      stream.markdown("Open a `.jaml` file first, then try `@jimbo /explain` again.");
      return;
    }
    userPrompt =
      `Explain this JAML filter in plain English — what kind of Balatro seeds would it match?\n\`\`\`yaml\n${editor.document.getText()}\n\`\`\``;
  } else if (request.command === "search") {
    if (!isJaml) {
      stream.markdown("Open a `.jaml` file first, then try `@jimbo /search` again.");
      return;
    }
    await runCurrentFilterSearch(extensionContext, { openPanel: true, stream });
    return; // Don't forward this to Copilot, we handle it natively!
  } else if (request.command === "analyze") {
    const sel = editor?.document.getText(editor.selection)?.trim() ?? "";
    const seed = normalizeSeedInput(sel || request.prompt);
    if (!seed) {
      stream.markdown(
        "Select or type a valid Balatro seed (1-8 characters, `[A-Z0-9]`), then try again.\n\nExample: `@jimbo /analyze 4CJQV`"
      );
      return;
    }
    userPrompt = `Analyze the Balatro seed **${seed}**: what jokers, bosses, and notable items would appear? What deck/strategy might work well?`;
  }

  const models = await vscode.lm.selectChatModels({ vendor: "copilot" });
  if (!models.length) {
    stream.markdown(
      "No Copilot language model available. Install the GitHub Copilot extension and sign in."
    );
    return;
  }

  const jamlSnippet = isJaml
    ? `\n\nThe user has this JAML filter open:\n\`\`\`yaml\n${editor.document.getText()}\n\`\`\``
    : "";

  const systemPrompt =
    `You are Jimbo, the mascot and expert assistant for JAML (Jimbo's Ante Markup Language) — the YAML-based filter language for Motely, a Balatro seed search engine. Help users write, understand, and improve JAML filters. Be concise and practical.${jamlSnippet}`;

  const messages = [
    vscode.LanguageModelChatMessage.User(systemPrompt),
    vscode.LanguageModelChatMessage.User(
      userPrompt || "Hello! How can you help me with JAML?"
    ),
  ];

  try {
    const response = await models[0].sendRequest(messages, {}, token);
    for await (const chunk of response.text) {
      stream.markdown(chunk);
    }
  } catch (err) {
    if (err?.name === "Cancelled") return;
    stream.markdown(`Language model error: ${err?.message ?? String(err)}`);
  }
}

// ---------------------------------------------------------------------------
// Seed analyzer — status bar button + command
// When selection looks like a Balatro seed ([A-Z0-9], 1-8 chars), the status
// bar lights up and right-click / command palette offer "Analyze Seed".
// ---------------------------------------------------------------------------
function registerSeedAnalyzer(context) {
  const statusBar = vscode.window.createStatusBarItem(
    vscode.StatusBarAlignment.Right,
    100
  );
  statusBar.command = "jaml.analyzeSeed";
  statusBar.tooltip = "Open this seed in Balatro Seed Curator";
  context.subscriptions.push(statusBar);

  const refresh = () => {
    const seed = getSelectedSeed();
    if (seed) {
      statusBar.text = `$(search) Analyze ${seed}`;
      statusBar.show();
    } else {
      statusBar.hide();
    }
  };

  context.subscriptions.push(
    vscode.window.onDidChangeTextEditorSelection(refresh)
  );
  context.subscriptions.push(
    vscode.window.onDidChangeActiveTextEditor(refresh)
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("jaml.analyzeSeed", async () => {
      let seed = getSelectedSeed();
      if (!seed) {
        seed = await vscode.window.showInputBox({
          prompt: "Enter a Balatro seed to analyze",
          placeHolder: "e.g. 4CJQV  (1-8 chars, A-Z and 0-9)",
          validateInput: (v) =>
            normalizeSeedInput(v)
              ? undefined
              : "Must be 1-8 characters using A-Z and 0-9",
        });
        if (!seed) return;
        seed = normalizeSeedInput(seed);
      }
      if (!seed) return;
      vscode.env.openExternal(
        vscode.Uri.parse(
          `https://jammy.seedfinder.app/?seed=${encodeURIComponent(seed)}`
        )
      );
    })
  );
}

function getSelectedSeed() {
  const editor = vscode.window.activeTextEditor;
  if (!editor) return null;
  return normalizeSeedInput(editor.document.getText(editor.selection));
}

// ---------------------------------------------------------------------------
// Sidebar — Activity Bar panel
// Filter browser + quick actions + settings
// ---------------------------------------------------------------------------
function registerSidebar(context) {
  const provider = new JamlSidebarProvider(context);
  context.subscriptions.push(
    vscode.window.registerWebviewViewProvider("jaml.sidebar", provider, {
      webviewOptions: { retainContextWhenHidden: true },
    })
  );
}

class JamlSidebarProvider {
  constructor(context) {
    this._context = context;
    this._view = null;
  }

  resolveWebviewView(webviewView) {
    this._view = webviewView;
    webviewView.webview.options = { enableScripts: true };
    webviewView.webview.html = this._getHtml();

    // Refresh filter list whenever workspace changes
    const refresh = () => this._postFilters();
    const watcher = vscode.workspace.createFileSystemWatcher("**/*.jaml");
    this._context.subscriptions.push(
      watcher.onDidCreate(refresh),
      watcher.onDidDelete(refresh),
      watcher
    );

    // Handle messages from webview
    webviewView.webview.onDidReceiveMessage(async (msg) => {
      switch (msg.type) {
        case "ready":
          this._postFilters();
          this._postSettings();
          break;
        case "openFilter":
          vscode.workspace.openTextDocument(vscode.Uri.file(msg.path))
            .then(doc => vscode.window.showTextDocument(doc));
          break;
        case "newFilter": {
          const folders = vscode.workspace.workspaceFolders;
          const base = folders?.[0]?.uri.fsPath ?? require("os").homedir();
          const uri = await vscode.window.showSaveDialog({
            defaultUri: vscode.Uri.file(require("path").join(base, "JamlFilters", "new.jaml")),
            filters: { "JAML Filter": ["jaml"] },
          });
          if (!uri) break;
          const template = `id: new-filter\nname: My Filter\ndeck: Red\nstake: White\nmust:\n  - joker: Any\n    antes: [1]\n`;
          await vscode.workspace.fs.writeFile(uri, Buffer.from(template));
          vscode.workspace.openTextDocument(uri).then(doc => vscode.window.showTextDocument(doc));
          this._postFilters();
          break;
        }
        case "openCurator": {
          const editor = vscode.window.activeTextEditor;
          const jaml = editor?.document.languageId === "jaml" ? editor.document.getText() : "";
          vscode.env.openExternal(
            vscode.Uri.parse(`https://jammy.seedfinder.app/${jaml ? "?jaml=" + encodeURIComponent(jaml) : ""}`)
          );
          break;
        }
        case "runSearch": {
          vscode.commands.executeCommand("jaml.searchCurrentFilter");
          break;
        }
        case "openSchema":
          vscode.commands.executeCommand("jaml.openSchema");
          break;
        case "saveSetting":
          vscode.workspace.getConfiguration("jaml").update(msg.key, msg.value, vscode.ConfigurationTarget.Global);
          break;
      }
    });
  }

  async _postFilters() {
    if (!this._view) return;
    const uris = await vscode.workspace.findFiles("**/*.jaml", "**/node_modules/**", 100);
    const filters = uris.map(u => ({
      path: u.fsPath,
      name: require("path").basename(u.fsPath, ".jaml"),
    })).sort((a, b) => a.name.localeCompare(b.name));
    this._view.webview.postMessage({ type: "filters", filters });
  }

  _postSettings() {
    if (!this._view) return;
    const cfg = vscode.workspace.getConfiguration("jaml");
    this._view.webview.postMessage({
      type: "settings",
      threads: cfg.get("search.threads", 8),
      batchSize: cfg.get("search.batchSize", 6),
      quickBatchCount: cfg.get("search.quickBatchCount", 25),
      maxResults: cfg.get("search.maxResults", 100),
      resultsPath: cfg.get("search.resultsPath", ""),
    });
  }

  _getHtml() {
    return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; script-src 'unsafe-inline';">
<style>
  :root {
    --bal-red:    #fe5f55;
    --bal-blue:   #4fc3f7;
    --bal-orange: #ffb347;
    --bal-grey:   #8a8fa8;
    --bal-dark:   #1a1025;
    --bal-purple: #22142f;
    --radius: 6px;
  }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body {
    background: var(--vscode-sideBar-background, var(--bal-dark));
    color: var(--vscode-foreground, #cdd6f4);
    font-family: var(--vscode-font-family, sans-serif);
    font-size: 12px;
    padding: 8px 6px;
    display: flex;
    flex-direction: column;
    gap: 10px;
  }
  h3 {
    font-size: 10px;
    text-transform: uppercase;
    letter-spacing: .08em;
    color: var(--bal-grey);
    margin-bottom: 4px;
  }
  .btn {
    display: flex;
    align-items: center;
    gap: 6px;
    width: 100%;
    padding: 6px 8px;
    border: none;
    border-radius: var(--radius);
    cursor: pointer;
    font-size: 12px;
    font-family: inherit;
    transition: opacity .15s;
  }
  .btn:hover { opacity: .85; }
  .btn-red    { background: var(--bal-red);    color: #fff; }
  .btn-blue   { background: var(--bal-blue);   color: #1a1a2e; }
  .btn-orange { background: var(--bal-orange); color: #1a1a2e; }
  .btn-ghost  {
    background: transparent;
    color: var(--vscode-foreground);
    border: 1px solid var(--vscode-panel-border, #3a3a5a);
  }
  .filter-list {
    display: flex;
    flex-direction: column;
    gap: 2px;
    max-height: 240px;
    overflow-y: auto;
  }
  .filter-item {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 5px 8px;
    border-radius: var(--radius);
    cursor: pointer;
    border: none;
    background: transparent;
    color: var(--vscode-foreground);
    font-size: 12px;
    font-family: inherit;
    text-align: left;
    width: 100%;
  }
  .filter-item:hover { background: var(--vscode-list-hoverBackground, #ffffff12); }
  .filter-item .dot { width: 7px; height: 7px; border-radius: 50%; background: var(--bal-orange); flex-shrink: 0; }
  .section { display: flex; flex-direction: column; gap: 4px; }
  .divider { height: 1px; background: var(--vscode-panel-border, #3a3a5a); }
  .row { display: flex; gap: 6px; align-items: center; }
  .row label { flex: 1; color: var(--bal-grey); }
  .row input[type=number] {
    width: 60px;
    background: var(--vscode-input-background, #1e1e2e);
    border: 1px solid var(--vscode-input-border, #3a3a5a);
    color: var(--vscode-input-foreground, #cdd6f4);
    border-radius: 4px;
    padding: 3px 6px;
    font-size: 12px;
    font-family: inherit;
  }
  .empty { color: var(--bal-grey); padding: 6px 8px; font-style: italic; }
</style>
</head>
<body>
<div class="section">
  <button class="btn btn-red" onclick="send('runSearch')">⚡ Quick Search</button>
  <button class="btn btn-blue" onclick="send('openCurator')">🌐 Open in Seed Curator</button>
</div>

<div class="divider"></div>

<div class="section">
  <h3>Filter Browser</h3>
  <div class="filter-list" id="filterList"><div class="empty">Scanning workspace…</div></div>
  <button class="btn btn-ghost" onclick="send('newFilter')" style="margin-top:2px">+ New Filter</button>
</div>

<div class="divider"></div>

<div class="section">
  <h3>Search Settings</h3>
  <div class="row">
    <label>Threads</label>
    <input type="number" id="threads" min="1" max="32" value="8" onchange="saveSetting('search.threads', +this.value)">
  </div>
  <div class="row">
    <label>Batch Char Count</label>
    <input type="number" id="batchSize" min="1" max="7" value="6" onchange="saveSetting('search.batchSize', +this.value)">
  </div>
  <div class="row">
    <label>Quick Batch Count</label>
    <input type="number" id="quickBatchCount" min="1" max="1000000" value="25" onchange="saveSetting('search.quickBatchCount', +this.value)">
  </div>
  <div class="row">
    <label>Max Results</label>
    <input type="number" id="maxResults" min="1" max="1000" value="100" onchange="saveSetting('search.maxResults', +this.value)">
  </div>
</div>

<div class="divider"></div>

<div class="section">
  <h3>Results</h3>
  <div class="row">
    <label>Results Folder</label>
  </div>
  <div class="row">
    <input type="text" id="resultsPath" value="" style="width:100%; background: var(--vscode-input-background, #1e1e2e); border: 1px solid var(--vscode-input-border, #3a3a5a); color: var(--vscode-input-foreground, #cdd6f4); border-radius: 4px; padding: 3px 6px; font-size: 12px; font-family: inherit;" onchange="saveSetting('search.resultsPath', this.value)">
  </div>
</div>

<div class="divider"></div>

<div class="section">
  <button class="btn btn-ghost" onclick="send('openSchema')">📋 Open JAML Schema</button>
</div>

<script>
const vscode = acquireVsCodeApi();
function send(type, extra) { vscode.postMessage({ type, ...extra }); }
function saveSetting(key, value) { vscode.postMessage({ type: 'saveSetting', key, value }); }

window.addEventListener('message', e => {
  const msg = e.data;
  if (msg.type === 'filters') {
    const list = document.getElementById('filterList');
    if (!msg.filters.length) {
      list.innerHTML = '<div class="empty">No .jaml files in workspace</div>';
      return;
    }
    list.innerHTML = msg.filters.map(f =>
      \`<button class="filter-item" onclick="send('openFilter',{path:\${JSON.stringify(f.path)}})">
        <span class="dot"></span><span>\${f.name}</span>
      </button>\`
    ).join('');
  }
  if (msg.type === 'settings') {
    document.getElementById('threads').value = msg.threads;
    document.getElementById('batchSize').value = msg.batchSize;
    document.getElementById('quickBatchCount').value = msg.quickBatchCount;
    document.getElementById('maxResults').value = msg.maxResults;
    document.getElementById('resultsPath').value = msg.resultsPath ?? '';
  }
});
send('ready');
</script>
</body>
</html>`;
  }
}

module.exports = { activate, deactivate };
