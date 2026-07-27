/**
 * @jimbo chat participant.
 *
 * Owns the Copilot Chat turn when the user @-mentions jimbo.
 * J1 validate + J2 search call real Motely (Lsp --diagnose / CLI --collect).
 *
 * Shape follows Microsoft's Chat Participant API:
 * https://code.visualstudio.com/api/extension-guides/ai/chat
 */
import * as vscode from "vscode";
import {
  diagnoseJaml,
  explainTopic,
  formatDiagnoseMarkdown,
  formatSearchMarkdown,
  searchSeeds,
} from "./motelyEngine";

export const JIMBO_PARTICIPANT_ID = "jaml.jimbo";

const SYSTEM = [
  "You are Jimbo — Motely/JAML assistant for Balatro seed filters.",
  "One grammar: Motely engine only. Never invent seeds.",
  "Prefer tools: validateJaml before findSeeds. Report only engine results.",
  "Be brief. Prefer tables and code blocks over fluff.",
].join(" ");

export function registerJimboChat(context: vscode.ExtensionContext): vscode.ChatParticipant {
  const handler: vscode.ChatRequestHandler = async (
    request: vscode.ChatRequest,
    chatContext: vscode.ChatContext,
    stream: vscode.ChatResponseStream,
    token: vscode.CancellationToken,
  ) => {
    const slash = request.command ? `/${request.command}` : "(none)";
    stream.progress(`Jimbo listening… command=${slash}`);

    // J1: real engine validate via Motely.Lsp --diagnose
    if (request.command === "validate") {
      stream.progress("Running Motely engine validate…");
      try {
        const editor = vscode.window.activeTextEditor;
        const fromEditor =
          editor &&
          (editor.document.languageId === "jaml" || editor.document.fileName.endsWith(".jaml"))
            ? editor.document.uri.scheme === "file"
              ? { filePath: editor.document.uri.fsPath, label: editor.document.uri.fsPath }
              : { jamlText: editor.document.getText(), label: editor.document.uri.toString() }
            : undefined;
        const input = request.prompt?.trim()
          ? { jamlText: request.prompt, label: "(chat prompt as JAML)" }
          : fromEditor;
        if (!input) {
          stream.markdown(
            "Open a `.jaml` file or paste JAML after `/validate`.\n\nEngine: `Motely.Lsp --diagnose`.",
          );
          return { metadata: { command: "validate" } };
        }
        const result = await diagnoseJaml(context, input);
        stream.markdown(formatDiagnoseMarkdown(result, input.label));
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        stream.markdown(`**Validate failed:** ${msg}`);
      }
      return { metadata: { command: "validate" } };
    }

    // J2: real Motely.CLI --collect
    if (request.command === "find") {
      stream.progress("Running Motely.CLI --collect…");
      try {
        const editor = vscode.window.activeTextEditor;
        const fromEditor =
          editor &&
          (editor.document.languageId === "jaml" || editor.document.fileName.endsWith(".jaml"))
            ? editor.document.uri.scheme === "file"
              ? { filePath: editor.document.uri.fsPath, label: editor.document.uri.fsPath }
              : { jamlText: editor.document.getText(), label: editor.document.uri.toString() }
            : undefined;

        // Optional: "/find 5" or "/find collect 5" → collectN
        let collectN = 1;
        let promptBody = request.prompt?.trim() ?? "";
        const nMatch = promptBody.match(/^(?:collect\s+)?(\d+)\s*$/i);
        if (nMatch) {
          collectN = Math.max(1, Math.min(Number(nMatch[1]), 100));
          promptBody = "";
        } else {
          const embed = promptBody.match(/\bcollect\s+(\d+)\b/i);
          if (embed) {
            collectN = Math.max(1, Math.min(Number(embed[1]), 100));
          }
        }

        const looksLikeJaml =
          promptBody.includes("must:") ||
          promptBody.includes("should:") ||
          promptBody.includes("deck:") ||
          promptBody.includes("\n- ");
        const input = looksLikeJaml
          ? { jamlText: promptBody, label: "(chat prompt as JAML)" }
          : fromEditor;
        if (!input) {
          stream.markdown(
            "Open a `.jaml` file (or paste full JAML after `/find`).\n\n" +
              "Examples: `@jimbo /find` · `@jimbo /find 3`\n\n" +
              "Engine: `Motely.CLI --collect N` (temp copy — does not rewrite your file).",
          );
          return { metadata: { command: "find" } };
        }
        const result = await searchSeeds(context, { ...input, collectN });
        stream.markdown(formatSearchMarkdown(result, input.label));
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        stream.markdown(`**Find failed:** ${msg}`);
      }
      return { metadata: { command: "find" } };
    }

    // J3: engine schema explain
    if (request.command === "explain") {
      stream.progress("Looking up Motely JAML schema…");
      try {
        let topic = request.prompt?.trim() ?? "";
        if (!topic) {
          const editor = vscode.window.activeTextEditor;
          if (editor) {
            const sel = editor.document.getText(editor.selection).trim();
            if (sel) {
              topic = sel;
            } else {
              const wordRange = editor.document.getWordRangeAtPosition(editor.selection.active);
              if (wordRange) {
                topic = editor.document.getText(wordRange);
              }
            }
          }
        }
        if (!topic) {
          stream.markdown(
            "Usage: `@jimbo /explain joker` · `/explain must` · `/explain Perkeo`\n\n" +
              "Or select a word in a `.jaml` file and run `/explain`.",
          );
          return { metadata: { command: "explain" } };
        }
        const result = await explainTopic(context, topic);
        if (!result.ok || !result.markdown) {
          stream.markdown(
            `Unknown topic \`${topic}\` (via \`${result.via}\`).\n\n` +
              "Try: `joker`, `voucher`, `pokerHand`, `must`, `Perkeo`, `joker blue`.",
          );
        } else {
          stream.markdown(result.markdown + `\n\n_via \`${result.via}\`_`);
        }
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        stream.markdown(`**Explain failed:** ${msg}`);
      }
      return { metadata: { command: "explain" } };
    }

    // Default: optional LM echo if the host gave us a model (Copilot chat).
    const prompt = request.prompt?.trim() || "ping";
    if (request.model) {
      try {
        const messages = [
          vscode.LanguageModelChatMessage.User(SYSTEM),
          ...historyAsMessages(chatContext),
          vscode.LanguageModelChatMessage.User(prompt),
        ];
        const response = await request.model.sendRequest(messages, {}, token);
        for await (const fragment of response.text) {
          stream.markdown(fragment);
        }
        return { metadata: { command: "chat" } };
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        stream.markdown(`_Model request failed:_ ${msg}\n\n`);
      }
    }

    stream.markdown(
      [
        `**Jimbo J0 online.**`,
        "",
        `| | |`,
        `|--|--|`,
        `| you said | \`${escapePipes(prompt)}\` |`,
        `| slash | \`${slash}\` |`,
        `| model | ${request.model ? "yes" : "none (install Copilot Chat for LM)"} |`,
        "",
        "Try: `@jimbo /find` · `@jimbo /validate` · `@jimbo /explain`",
        "",
        "Scaffold only — seed search lands in J2.",
      ].join("\n"),
    );
    return { metadata: { command: "pong" } };
  };

  const participant = vscode.chat.createChatParticipant(JIMBO_PARTICIPANT_ID, handler);
  participant.iconPath = vscode.Uri.joinPath(context.extensionUri, "media", "jimbo.svg");

  participant.followupProvider = {
    provideFollowups(result) {
      const cmd = (result.metadata as { command?: string } | undefined)?.command;
      if (cmd === "find" || cmd === "validate") {
        return [
          { prompt: "Explain must vs should", label: "Explain must/should" },
          { prompt: "/find collect 1", label: "Find one seed (when J2 ships)" },
        ];
      }
      return [
        { prompt: "/validate", label: "Validate JAML" },
        { prompt: "/find", label: "Find seeds" },
      ];
    },
  };

  context.subscriptions.push(participant);
  return participant;
}

function historyAsMessages(context: vscode.ChatContext): vscode.LanguageModelChatMessage[] {
  const out: vscode.LanguageModelChatMessage[] = [];
  for (const turn of context.history) {
    if (turn instanceof vscode.ChatRequestTurn) {
      out.push(vscode.LanguageModelChatMessage.User(turn.prompt));
    } else if (turn instanceof vscode.ChatResponseTurn) {
      let text = "";
      for (const part of turn.response) {
        if (part instanceof vscode.ChatResponseMarkdownPart) {
          text += part.value.value;
        }
      }
      if (text) {
        out.push(vscode.LanguageModelChatMessage.Assistant(text));
      }
    }
  }
  // Keep context bounded in scaffold.
  return out.slice(-8);
}

function escapePipes(s: string): string {
  return s.replace(/\|/g, "\\|").replace(/\n/g, " ");
}
