const vscode = require("vscode");
const path = require("path");
const fs = require("fs");

const JAML_SELECTOR = [{ language: "jaml", scheme: "file" }, { language: "jaml", scheme: "untitled" }];
const JUMMY_SELECTOR = [{ language: "jummy", scheme: "file" }, { language: "jummy", scheme: "untitled" }];
const DIAGNOSTIC_SOURCE = "jaml-language-support";

function activate(context) {
  const core = loadLanguageCore(context);
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

  context.subscriptions.push(vscode.languages.registerCompletionItemProvider(JAML_SELECTOR, createCompletionProvider(context), " ", "\n", ":"));
  context.subscriptions.push(vscode.languages.registerHoverProvider(JAML_SELECTOR, createHoverProvider(context)));
  context.subscriptions.push(vscode.languages.registerCodeActionsProvider(JAML_SELECTOR, createCodeActionProvider(), { providedCodeActionKinds: [vscode.CodeActionKind.QuickFix] }));

  context.subscriptions.push(vscode.commands.registerCommand("jaml.showDocumentSummary", () => showDocumentSummary(core)));
  context.subscriptions.push(vscode.commands.registerCommand("jaml.openSchema", () => openBundledSchema(context)));
}

function deactivate() { }

function loadLanguageCore(context) {
  const corePath = path.join(context.extensionPath, "vendor", "jaml-language-core", "index.js");
  if (!fs.existsSync(corePath)) {
    return undefined;
  }

  const source = fs.readFileSync(corePath, "utf8");
  const exports = {};
  const transformed = source.replace(/export const /g, "const ").replace(/export function /g, "function ");
  const footer = "\nObject.assign(exports, { JAML_CRITERION_SECTION_KEYS, analyzeJamlText });";
  Function("exports", `${transformed}${footer}`)(exports);
  return exports;
}

function updateDiagnostics(document, collection, core) {
  if (document.languageId !== "jaml") {
    return;
  }

  const diagnostics = [];
  if (core && typeof core.analyzeJamlText === "function") {
    for (const diagnostic of core.analyzeJamlText(document.getText())) {
      diagnostics.push(toVsCodeDiagnostic(diagnostic));
    }
  }

  collection.set(document.uri, diagnostics);
}

function toVsCodeDiagnostic(diagnostic) {
  const range = new vscode.Range(
    diagnostic.range.start.line,
    diagnostic.range.start.character,
    diagnostic.range.end.line === Number.MAX_SAFE_INTEGER ? diagnostic.range.start.line : diagnostic.range.end.line,
    diagnostic.range.end.character === Number.MAX_SAFE_INTEGER ? 200 : diagnostic.range.end.character
  );
  const result = new vscode.Diagnostic(range, diagnostic.message, toSeverity(diagnostic.severity));
  result.source = diagnostic.source || DIAGNOSTIC_SOURCE;
  result.code = diagnostic.code;
  return result;
}

function toSeverity(severity) {
  switch (severity) {
    case "error": return vscode.DiagnosticSeverity.Error;
    case "warning": return vscode.DiagnosticSeverity.Warning;
    case "information": return vscode.DiagnosticSeverity.Information;
    case "hint": return vscode.DiagnosticSeverity.Hint;
    default: return vscode.DiagnosticSeverity.Information;
  }
}

function createCompletionProvider(context) {
  return {
    provideCompletionItems(document, position) {
      const linePrefix = document.lineAt(position).text.slice(0, position.character);
      if (/^\s*$/.test(linePrefix)) {
        return rootCompletions();
      }
      return criterionCompletions(context);
    }
  };
}

function rootCompletions() {
  return ["deck", "stake", "must", "should", "mustNot", "defaults"].map(key => {
    const item = new vscode.CompletionItem(key, vscode.CompletionItemKind.Property);
    item.insertText = `${key}: `;
    return item;
  });
}

function criterionCompletions(context) {
  const schema = readSchema(context);
  const properties = schema?.$defs?.JamlCriterion?.properties || {};
  return Object.keys(properties).sort().map(key => {
    const item = new vscode.CompletionItem(key, vscode.CompletionItemKind.Property);
    item.insertText = `${key}: `;
    const description = properties[key].description;
    if (description) {
      item.documentation = new vscode.MarkdownString(description);
    }
    return item;
  });
}

function createHoverProvider(context) {
  return {
    provideHover(document, position) {
      const range = document.getWordRangeAtPosition(position, /[A-Za-z][A-Za-z0-9]*/);
      if (!range) {
        return undefined;
      }
      const word = document.getText(range);
      const schema = readSchema(context);
      const criterion = schema?.$defs?.JamlCriterion?.properties?.[word];
      if (criterion) {
        return new vscode.Hover(new vscode.MarkdownString(`**JAML criterion** \`${word}\``), range);
      }
      if (["must", "should", "mustNot"].includes(word)) {
        return new vscode.Hover(new vscode.MarkdownString(`**JAML section** \`${word}\` uses the shared \`JamlCriterion\` shape.`), range);
      }
      return undefined;
    }
  };
}

function createCodeActionProvider() {
  return {
    provideCodeActions(document, range, context) {
      return context.diagnostics
        .filter(diagnostic => diagnostic.source === "jaml-language-core")
        .map(diagnostic => {
          const action = new vscode.CodeAction("Show JAML summary", vscode.CodeActionKind.QuickFix);
          action.command = { command: "jaml.showDocumentSummary", title: "Show JAML summary" };
          action.diagnostics = [diagnostic];
          return action;
        });
    }
  };
}

function showDocumentSummary(core) {
  const editor = vscode.window.activeTextEditor;
  if (!editor || editor.document.languageId !== "jaml") {
    vscode.window.showInformationMessage("Open a .jaml file first.");
    return;
  }

  const text = editor.document.getText();
  const warnings = core && typeof core.analyzeJamlText === "function" ? core.analyzeJamlText(text).length : 0;
  const must = countSectionItems(text, "must");
  const should = countSectionItems(text, "should");
  const mustNot = countSectionItems(text, "mustNot");
  vscode.window.showInformationMessage(`JAML summary: must ${must}, should ${should}, mustNot ${mustNot}, editor diagnostics ${warnings}.`);
}

function countSectionItems(text, section) {
  const sectionPattern = new RegExp(`^${section}:\\s*\\r?\\n([\\s\\S]*?)(?=^[A-Za-z][A-Za-z0-9]*:\\s*$|$)`, "m");
  const match = text.match(sectionPattern);
  if (!match) {
    return 0;
  }
  return (match[1].match(/^\s*-\s+/gm) || []).length;
}

function openBundledSchema(context) {
  const schemaPath = path.join(context.extensionPath, "schema", "jaml.schema.json");
  vscode.workspace.openTextDocument(schemaPath).then(document => vscode.window.showTextDocument(document));
}

function readSchema(context) {
  const schemaPath = path.join(context.extensionPath, "schema", "jaml.schema.json");
  try {
    return JSON.parse(fs.readFileSync(schemaPath, "utf8"));
  } catch {
    return undefined;
  }
}

module.exports = { activate, deactivate };
