/**
 * JAML Language Support — VS Code Extension
 *
 * - Starts the jaml-lsp-server as a child process (LSP over Node IPC)
 * - Registers the Balatro Seed Curator MCP server into Copilot Chat
 * - Keeps syntax highlighting, snippets, and commands
 */

"use strict";

const vscode = require("vscode");
const path = require("path");
const { LanguageClient, TransportKind } = require("vscode-languageclient/node");

const MCP_SERVER_ID = "balatro-seed-curator";
const MCP_SERVER_URL = "https://mcp.seedfinder.app/mcp";

let client;

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

// ---------------------------------------------------------------------------
// Chat participant — @jimbo
// VS Code 1.97+ Language Model API
// ---------------------------------------------------------------------------
function registerChatParticipant(context) {
  if (!vscode.chat?.createChatParticipant) return; // VS Code < 1.97

  const participant = vscode.chat.createChatParticipant(
    "jaml.jimbo",
    handleJimboChat
  );
  participant.iconPath = vscode.Uri.joinPath(
    context.extensionUri,
    "images",
    "icon.ico"
  );
  context.subscriptions.push(participant);
}

async function handleJimboChat(request, _context, stream, token) {
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
    try {
      const motelyMod = await import("motely-wasm");
      if (typeof motelyMod.default?.boot === "function") await motelyMod.default.boot();
      else if (typeof motelyMod.default?.initialize === "function") await motelyMod.default.initialize();
      const motely = motelyMod.MotelyWasm ?? motelyMod.default ?? motelyMod;
      const text = editor.document.getText();
      const labels = motely.getTallyLabels(text) ?? [];
      const batchResult = await motely.runSequentialSearchBatch(text, 6, 0n, 500000n, 10);
      
      if (!batchResult || !batchResult.results || batchResult.results.length === 0) {
        stream.markdown("No results found in a quick scan of the first 500,000 seeds.");
        return;
      }
      
      let md = `**Quick Search Results** *(First ${batchResult.results.length} found)*\n\n`;
      md += `| Seed | Score | ${labels.join(" | ")} |\n`;
      md += `|---|---|${labels.map(() => "---").join("|")} |\n`;
      for (const hit of batchResult.results) {
        md += `| **${hit.seed}** | ${hit.score} | ${Array.from(hit.tallyColumns).join(" | ")} |\n`;
      }
      stream.markdown(md);
    } catch (err) {
      stream.markdown(`Error running search: ${err?.message ?? err}`);
    }
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
          const editor = vscode.window.activeTextEditor;
          if (!editor || editor.document.languageId !== "jaml") {
            vscode.window.showWarningMessage("Open a .jaml file first.");
            break;
          }
          vscode.commands.executeCommand("workbench.action.chat.open", { query: "@jimbo /search" });
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
    <input type="number" id="batchSize" min="1" max="10" value="6" onchange="saveSetting('search.batchSize', +this.value)">
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
  }
});
send('ready');
</script>
</body>
</html>`;
  }
}

module.exports = { activate, deactivate };
