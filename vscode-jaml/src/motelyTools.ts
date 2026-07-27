/**
 * Language model tools (MS Tools API):
 *  J1 motely_validate_jaml
 *  J2 motely_search_seeds
 * https://code.visualstudio.com/api/extension-guides/ai/tools
 */
import * as fs from "node:fs";
import * as vscode from "vscode";
import {
  diagnoseJaml,
  formatDiagnoseMarkdown,
  formatSearchMarkdown,
  searchSeeds,
} from "./motelyEngine";

interface ValidateParams {
  jamlText?: string;
  filePath?: string;
}

interface SearchParams {
  jamlText?: string;
  filePath?: string;
  collectN?: number;
}

export function registerMotelyTools(context: vscode.ExtensionContext): void {
  context.subscriptions.push(
    vscode.lm.registerTool("motely_validate_jaml", new ValidateJamlTool(context)),
    vscode.lm.registerTool("motely_search_seeds", new SearchSeedsTool(context)),
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
      const input = await resolveJamlInput(options.input);
      const result = await diagnoseJaml(this.context, input);
      const md = formatDiagnoseMarkdown(result, input.label);
      return new vscode.LanguageModelToolResult([new vscode.LanguageModelTextPart(md)]);
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      throw new Error(
        `motely_validate_jaml failed: ${msg}. Ensure Motely.Lsp is available. Retry with absolute filePath or full jamlText.`,
      );
    }
  }
}

class SearchSeedsTool implements vscode.LanguageModelTool<SearchParams> {
  constructor(private readonly context: vscode.ExtensionContext) {}

  async prepareInvocation(
    options: vscode.LanguageModelToolInvocationPrepareOptions<SearchParams>,
    _token: vscode.CancellationToken,
  ) {
    const n = options.input.collectN ?? 1;
    const where =
      options.input.filePath?.trim() ||
      (options.input.jamlText !== undefined ? "pasted JAML" : "active .jaml");
    return {
      invocationMessage: `Motely seed search (collect ${n})`,
      confirmationMessages: {
        title: "Find Balatro seeds",
        message: new vscode.MarkdownString(
          `Run **Motely.CLI --collect ${n}** on **${where}**?\n\n` +
            `Local CPU search (can take seconds–minutes). Filter is copied to a temp file so your disk file is not rewritten.`,
        ),
      },
    };
  }

  async invoke(
    options: vscode.LanguageModelToolInvocationOptions<SearchParams>,
    _token: vscode.CancellationToken,
  ) {
    try {
      const input = await resolveJamlInput(options.input);
      const result = await searchSeeds(this.context, {
        ...input,
        collectN: options.input.collectN,
      });
      const md = formatSearchMarkdown(result, input.label);
      return new vscode.LanguageModelToolResult([new vscode.LanguageModelTextPart(md)]);
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      throw new Error(
        `motely_search_seeds failed: ${msg}. Validate JAML first. Need Motely.CLI (jaml.cliPath or MotelyJAML workspace).`,
      );
    }
  }
}

async function resolveJamlInput(
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
