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
      const schemaPath = path.join(context.extensionPath, "schema", "jaml.schema.json");
      vscode.workspace
        .openTextDocument(schemaPath)
        .then((doc) => vscode.window.showTextDocument(doc));
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

// --- legacy dead code below this line, kept only for reference, never called ---
function _unused_activate(context) {
  const core = _unused_loadLanguageCore(context);
  const diagnostics = vscode.languages.createDiagnosticCollection("jaml");
  context.subscriptions.push(diagnostics);

  const refreshActiveDocument = () => {
    const editor = vscode.window.activeTextEditor;
    if (editor) {
      updateDiagnostics(editor.document, diagnostics, core);
    }
  };

  for (const document of vscode.workspace.textDocuments) {
    updateDiagnostics(document, diagnostics, core);
  }

  context.subscriptions.push(vscode.workspace.onDidOpenTextDocument(document => updateDiagnostics(document, diagnostics, core)));
  context.subscriptions.push(vscode.workspace.onDidChangeTextDocument(event => updateDiagnostics(event.document, diagnostics, core)));
  context.subscriptions.push(vscode.workspace.onDidCloseTextDocument(document => diagnostics.delete(document.uri)));
  context.subscriptions.push(vscode.window.onDidChangeActiveTextEditor(refreshActiveDocument));

}

module.exports = { activate, deactivate };
