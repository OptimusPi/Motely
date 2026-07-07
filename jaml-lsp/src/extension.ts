import * as vscode from "vscode";
import { validate, getCompletions, getHover } from "jaml-lang";
import { registerJamlChatParticipant } from "./chatParticipant.js";
import { init as initSearch, runSearch, stopSearch } from "./searchRunner.js";
import { ResultsPanel } from "./resultsPanel.js";
import { JamlNotebookSerializer, JamlNotebookExecutor } from "./notebookProvider.js";

const JAML_LANG = "jaml";
let diagnosticCollection: vscode.DiagnosticCollection;

export function activate(ctx: vscode.ExtensionContext) {
  diagnosticCollection = vscode.languages.createDiagnosticCollection(JAML_LANG);
  ctx.subscriptions.push(diagnosticCollection);

  registerJamlChatParticipant(ctx);
  registerSeedSearch(ctx);

  // Diagnostics on open + change
  ctx.subscriptions.push(
    vscode.workspace.onDidOpenTextDocument(validateDoc),
    vscode.workspace.onDidChangeTextDocument((e) => validateDoc(e.document)),
    vscode.workspace.onDidCloseTextDocument((doc) => diagnosticCollection.delete(doc.uri)),
  );
  vscode.workspace.textDocuments.forEach(validateDoc);

  // Completions
  ctx.subscriptions.push(
    vscode.languages.registerCompletionItemProvider(
      JAML_LANG,
      {
        provideCompletionItems(doc, pos) {
          if (!vscode.workspace.getConfiguration("jaml").get("validate", true)) return [];
          const offset = doc.offsetAt(pos);
          const items = getCompletions(doc.getText(), offset);
          return items.map((item) => {
            const ci = new vscode.CompletionItem(item.label);
            ci.kind =
              item.kind === "keyword" ? vscode.CompletionItemKind.Keyword
              : item.kind === "enum"  ? vscode.CompletionItemKind.EnumMember
              : item.kind === "field" ? vscode.CompletionItemKind.Field
              :                        vscode.CompletionItemKind.Constant;
            if (item.detail) ci.detail = item.detail;
            return ci;
          });
        },
      },
      ":",
      " ",
      "\n",
    ),
  );

  // Hover
  ctx.subscriptions.push(
    vscode.languages.registerHoverProvider(JAML_LANG, {
      provideHover(doc, pos) {
        const offset = doc.offsetAt(pos);
        const info = getHover(doc.getText(), offset);
        if (!info) return null;
        return new vscode.Hover(new vscode.MarkdownString(info.markdown));
      },
    }),
  );
}

// Seed search + notebook + CodeLens — the WASM-backed features, restored from v1.2.0 and
// rewired to the real motely-wasm@23.x API (see searchRunner.ts). The run button appears on
// line 1 of every .jaml/.jummy file; .jamlnb notebooks run the same engine per cell.
function registerSeedSearch(ctx: vscode.ExtensionContext) {
  initSearch(ctx.extensionPath);

  ctx.subscriptions.push(
    vscode.languages.registerCodeLensProvider(
      [{ language: "jaml" }, { language: "jummy" }],
      {
        provideCodeLenses(doc) {
          return [
            new vscode.CodeLens(new vscode.Range(0, 0, 0, 0), {
              title: "▶ Run Search (1M seeds)",
              command: "jaml.runSearch",
              arguments: [doc],
            }),
          ];
        },
      },
    ),
  );

  ctx.subscriptions.push(
    vscode.commands.registerCommand("jaml.runSearch", async (docArg?: vscode.TextDocument) => {
      const doc = docArg ?? vscode.window.activeTextEditor?.document;
      if (!doc) return;

      const panel = ResultsPanel.getOrCreate(ctx.extensionUri);
      const jaml = doc.getText();
      panel.searching(jaml.slice(0, 60).replace(/\n/g, "  ").trim() + "…");

      try {
        await runSearch(
          jaml,
          1_000_000,
          (_s, m) => vscode.window.setStatusBarMessage(`JAML: ${m} hits…`, 500),
          (seed, score) => panel.addResult(seed, score),
          (summary) => {
            panel.done(summary);
            vscode.window.setStatusBarMessage(
              `JAML: ${summary.matched} matches in ${summary.searched} seeds (${summary.elapsedMs}ms)`,
              5000,
            );
          },
        );
      } catch (err) {
        panel.error((err as Error).message);
        vscode.window.showErrorMessage(`JAML Search error: ${(err as Error).message}`);
      }
    }),

    vscode.commands.registerCommand("jaml.stopSearch", () => {
      stopSearch();
      vscode.window.setStatusBarMessage("JAML: search stopped.", 2000);
    }),
  );

  ctx.subscriptions.push(
    vscode.workspace.registerNotebookSerializer("jaml-notebook", new JamlNotebookSerializer()),
  );
  new JamlNotebookExecutor().register(ctx);
}

export function deactivate() {
  diagnosticCollection?.dispose();
}

function validateDoc(doc: vscode.TextDocument) {
  if (doc.languageId !== JAML_LANG) return;
  if (!vscode.workspace.getConfiguration("jaml").get("validate", true)) return;
  const text = doc.getText();
  const raw = validate(text);
  const vsDiags = raw.map((d) => {
    const range = new vscode.Range(doc.positionAt(d.from), doc.positionAt(d.to));
    return new vscode.Diagnostic(
      range,
      d.message,
      d.severity === "error"
        ? vscode.DiagnosticSeverity.Error
        : vscode.DiagnosticSeverity.Warning,
    );
  });
  diagnosticCollection.set(doc.uri, vsDiags);
}
