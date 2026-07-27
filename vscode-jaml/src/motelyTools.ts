/**
 * Language model tools — J1: motely_validate_jaml (engine loader via Motely.Lsp --diagnose).
 * https://code.visualstudio.com/api/extension-guides/ai/tools
 */
import * as fs from "node:fs";
import * as vscode from "vscode";
import { diagnoseJaml, formatDiagnoseMarkdown } from "./motelyEngine";

interface ValidateParams {
  jamlText?: string;
  filePath?: string;
}

export function registerMotelyTools(context: vscode.ExtensionContext): void {
  context.subscriptions.push(
    vscode.lm.registerTool("motely_validate_jaml", new ValidateJamlTool(context)),
  );
}

class ValidateJamlTool implements vscode.LanguageModelTool<ValidateParams> {
  constructor(private readonly context: vscode.ExtensionContext) {}

  async prepareInvocation(
    options: vscode.LanguageModelToolInvocationPrepareOptions<ValidateParams>,
    _token: vscode.CancellationToken,
  ) {
    const where =
      options.input.filePath?.trim() ||
      (options.input.jamlText !== undefined ? "pasted JAML text" : "active editor?");
    return {
      invocationMessage: "Validating JAML with Motely engine",
      confirmationMessages: {
        title: "Validate JAML",
        message: new vscode.MarkdownString(
          `Run Motely engine validate on **${where}**? (local process, no network.)`,
        ),
      },
    };
  }

  async invoke(
    options: vscode.LanguageModelToolInvocationOptions<ValidateParams>,
    _token: vscode.CancellationToken,
  ) {
    try {
      const input = await resolveInput(options.input);
      const result = await diagnoseJaml(this.context, input);
      const md = formatDiagnoseMarkdown(result, input.label);
      return new vscode.LanguageModelToolResult([new vscode.LanguageModelTextPart(md)]);
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      // Message for the LLM: what failed and what to try next.
      throw new Error(
        `motely_validate_jaml failed: ${msg}. Ensure Motely.Lsp is published to vscode-jaml/server or the MotelyJAML workspace is open. Retry with filePath absolute or full jamlText.`,
      );
    }
  }
}

async function resolveInput(
  params: ValidateParams,
): Promise<{ jamlText?: string; filePath?: string; label: string }> {
  if (params.filePath?.trim()) {
    const filePath = params.filePath.trim();
    if (!fs.existsSync(filePath)) {
      throw new Error(`filePath does not exist: ${filePath}`);
    }
    return { filePath, label: filePath };
  }
  if (params.jamlText !== undefined && params.jamlText.length > 0) {
    return { jamlText: params.jamlText, label: "(inline text)" };
  }

  const editor = vscode.window.activeTextEditor;
  if (editor && (editor.document.languageId === "jaml" || editor.document.fileName.endsWith(".jaml"))) {
    if (editor.document.uri.scheme === "file") {
      return { filePath: editor.document.uri.fsPath, label: editor.document.uri.fsPath };
    }
    return { jamlText: editor.document.getText(), label: editor.document.uri.toString() };
  }

  throw new Error(
    "No jamlText, filePath, or active .jaml editor. Pass the document text or an absolute path.",
  );
}
