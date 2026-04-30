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
      const motely = motelyMod.MotelyWasm ?? motelyMod.default ?? motelyMod;
      if (typeof motely.initialize === "function") await motely.initialize();
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
    const seed = (sel || request.prompt.trim()).toUpperCase();
    if (!seed || !/^[1-9A-Z]{1,8}$/.test(seed)) {
      stream.markdown(
        "Select or type a valid Balatro seed (1–8 characters, `[1-9A-Z]`), then try again.\n\nExample: `@jimbo /analyze 4CJQV`"
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
// When selection looks like a Balatro seed ([1-9A-Z], 1–8 chars), the status
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
          placeHolder: "e.g. 4CJQV  (1–8 chars, A-Z and 1-9)",
          validateInput: (v) =>
            /^[1-9A-Z]{1,8}$/i.test(v.trim())
              ? undefined
              : "Must be 1–8 characters using A-Z and 1-9",
        });
        if (!seed) return;
        seed = seed.trim().toUpperCase();
      }
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
  const sel = editor.document.getText(editor.selection).trim().toUpperCase();
  return /^[1-9A-Z]{1,8}$/.test(sel) ? sel : null;
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
