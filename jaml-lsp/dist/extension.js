"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.activate = activate;
exports.deactivate = deactivate;
const vscode = require("vscode");
const jaml_lang_1 = require("jaml-lang");
const JAML_LANG = "jaml";
let diagnosticCollection;
function activate(ctx) {
    diagnosticCollection = vscode.languages.createDiagnosticCollection(JAML_LANG);
    ctx.subscriptions.push(diagnosticCollection);
    // Diagnostics on open + change
    ctx.subscriptions.push(vscode.workspace.onDidOpenTextDocument(validateDoc), vscode.workspace.onDidChangeTextDocument((e) => validateDoc(e.document)), vscode.workspace.onDidCloseTextDocument((doc) => diagnosticCollection.delete(doc.uri)));
    vscode.workspace.textDocuments.forEach(validateDoc);
    // Completions
    ctx.subscriptions.push(vscode.languages.registerCompletionItemProvider(JAML_LANG, {
        provideCompletionItems(doc, pos) {
            if (!vscode.workspace.getConfiguration("jaml").get("validate", true))
                return [];
            const offset = doc.offsetAt(pos);
            const items = (0, jaml_lang_1.getCompletions)(doc.getText(), offset);
            return items.map((item) => {
                const ci = new vscode.CompletionItem(item.label);
                ci.kind =
                    item.kind === "keyword" ? vscode.CompletionItemKind.Keyword
                        : item.kind === "enum" ? vscode.CompletionItemKind.EnumMember
                            : item.kind === "field" ? vscode.CompletionItemKind.Field
                                : vscode.CompletionItemKind.Constant;
                if (item.detail)
                    ci.detail = item.detail;
                return ci;
            });
        },
    }, ":", " ", "\n"));
    // Hover
    ctx.subscriptions.push(vscode.languages.registerHoverProvider(JAML_LANG, {
        provideHover(doc, pos) {
            const offset = doc.offsetAt(pos);
            const info = (0, jaml_lang_1.getHover)(doc.getText(), offset);
            if (!info)
                return null;
            return new vscode.Hover(new vscode.MarkdownString(info.markdown));
        },
    }));
}
function deactivate() {
    diagnosticCollection?.dispose();
}
function validateDoc(doc) {
    if (doc.languageId !== JAML_LANG)
        return;
    if (!vscode.workspace.getConfiguration("jaml").get("validate", true))
        return;
    const text = doc.getText();
    const raw = (0, jaml_lang_1.validate)(text);
    const vsDiags = raw.map((d) => {
        const range = new vscode.Range(doc.positionAt(d.from), doc.positionAt(d.to));
        return new vscode.Diagnostic(range, d.message, d.severity === "error"
            ? vscode.DiagnosticSeverity.Error
            : vscode.DiagnosticSeverity.Warning);
    });
    diagnosticCollection.set(doc.uri, vsDiags);
}
