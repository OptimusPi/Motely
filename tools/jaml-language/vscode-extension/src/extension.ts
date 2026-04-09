import * as path from "node:path";
import * as fs from "node:fs";
import * as vscode from "vscode";
import { LanguageClient, TransportKind } from "vscode-languageclient/node";
import { runSearch, stopSearch } from "./searchRunner.js";
import { JamlNotebookSerializer, JamlNotebookExecutor } from "./notebookProvider.js";

let client: LanguageClient | null = null;
let searchOutput: vscode.OutputChannel | null = null;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
  const extPath = context.extensionPath;
  searchOutput = vscode.window.createOutputChannel("JAML Search");
  context.subscriptions.push(searchOutput);

  // -- LSP ----------------------------------------------------------------
  const serverModule = path.join(extPath, "dist", "server.js");
  client = new LanguageClient(
    "jamlLanguageServer",
    "JAML Language Server",
    {
      run: { module: serverModule, transport: TransportKind.ipc },
      debug: { module: serverModule, transport: TransportKind.ipc },
    },
    { documentSelector: [{ scheme: "file", language: "jaml" }, { scheme: "file", language: "jummy" }] }
  );
  await client.start();
  context.subscriptions.push({ dispose: () => void client?.stop() });

  // -- CodeLens (play button on first line) --------------------------------
  context.subscriptions.push(
    vscode.languages.registerCodeLensProvider(
      [{ language: "jaml" }, { language: "jummy" }],
      {
        provideCodeLenses(doc) {
          return [
            new vscode.CodeLens(new vscode.Range(0, 0, 0, 0), {
              title: "\u25b6 Run Search (1M seeds)",
              command: "jaml.runSearch",
              arguments: [doc],
            }),
          ];
        },
      }
    )
  );

  // -- Commands ------------------------------------------------------------
  context.subscriptions.push(
    vscode.commands.registerCommand("jaml.runSearch", async (docArg?: vscode.TextDocument) => {
      const doc = docArg ?? vscode.window.activeTextEditor?.document;
      if (!doc) return;

      const jaml = doc.getText();
      const output = searchOutput ?? vscode.window.createOutputChannel("JAML Search");
      searchOutput = output;
      output.clear();
      output.show(true);
      output.appendLine("JAML Search");
      output.appendLine("");
      output.appendLine(`Filter: ${jaml.slice(0, 120).replace(/\s+/g, " ").trim()}`);
      output.appendLine("");

      try {
        await runSearch(
          jaml, 1_000_000,
          (_s, m) => vscode.window.setStatusBarMessage(`JAML: ${m} hits\u2026`, 500),
          (seed, score, tally) => {
            output.appendLine(
              `${seed}\t${score}\t[${Array.from(tally).join(", ")}]`
            );
          },
          (summary) => {
            output.appendLine("");
            output.appendLine(
              `Done: ${summary.matched} matches in ${summary.searched} seeds (${summary.elapsedMs}ms) status=${summary.status}`
            );
            vscode.window.setStatusBarMessage(
              `JAML: ${summary.matched} matches in ${summary.searched} seeds (${summary.elapsedMs}ms)`, 5000
            );
          }
        );
      } catch (err) {
        vscode.window.showErrorMessage(`JAML Search error: ${(err as Error).message}`);
      }
    }),

    vscode.commands.registerCommand("jaml.stopSearch", () => {
      stopSearch();
      vscode.window.setStatusBarMessage("JAML: search stopped.", 2000);
    }),

    vscode.commands.registerCommand("jaml.openExample", async () => {
      const examplesDir = path.join(extPath, "examples");
      if (!fs.existsSync(examplesDir)) {
        vscode.window.showErrorMessage("No example files found in extension.");
        return;
      }
      const files = fs.readdirSync(examplesDir).filter(f => f.endsWith(".jaml") || f.endsWith(".jummy"));
      const pick = await vscode.window.showQuickPick(
        files.map(f => ({ label: f.replace(/\.(jaml|jummy)$/, "").replace(/[-_]/g, " "), description: f })),
        { placeHolder: "Pick an example JAML filter to open" }
      );
      if (!pick) return;
      const src = path.join(examplesDir, pick.description!);
      const doc = await vscode.workspace.openTextDocument({ language: "jaml", content: fs.readFileSync(src, "utf8") });
      await vscode.window.showTextDocument(doc);
    })
  );

  // -- Notebook (.jamlnb) -------------------------------------------------
  context.subscriptions.push(
    vscode.workspace.registerNotebookSerializer("jaml-notebook", new JamlNotebookSerializer())
  );
  const executor = new JamlNotebookExecutor();
  executor.register(context);
}

export async function deactivate(): Promise<void> {
  if (client) {
    await client.stop();
    client = null;
  }
}
