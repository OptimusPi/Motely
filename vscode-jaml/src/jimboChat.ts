/**
 * @jimbo chat participant (J0 scaffold).
 *
 * Owns the Copilot Chat turn when the user @-mentions jimbo.
 * J1+ will call Motely tools (validate / search). This phase only proves
 * registration + streaming + slash command routing.
 *
 * Shape follows Microsoft's Chat Participant API:
 * https://code.visualstudio.com/api/extension-guides/ai/chat
 */
import * as vscode from "vscode";

export const JIMBO_PARTICIPANT_ID = "jaml.jimbo";

const SYSTEM = [
  "You are Jimbo — Motely/JAML assistant for Balatro seed filters.",
  "One grammar: Motely engine only. Do not invent seeds.",
  "This is scaffold phase J0: tools are not wired yet.",
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

    // Slash stubs until J1–J2 tools exist.
    if (request.command === "validate") {
      stream.markdown(
        [
          "**`/validate` (scaffold)**",
          "",
          "Next phase wires this to Motely `JamlConfigLoader` / LSP diagnose.",
          "Open a `.jaml` file and re-run after J1.",
          "",
          request.prompt ? `Your note: ${request.prompt}` : "",
        ]
          .filter(Boolean)
          .join("\n"),
      );
      return { metadata: { command: "validate" } };
    }

    if (request.command === "find") {
      stream.markdown(
        [
          "**`/find` (scaffold)**",
          "",
          "Next phase runs Motely search (`--collect N` / wasm) and returns **real** seeds.",
          "No fake seeds in this stub.",
          "",
          request.prompt ? `Filter intent: ${request.prompt}` : "Pass a JAML path or paste intent after J2.",
        ].join("\n"),
      );
      return { metadata: { command: "find" } };
    }

    if (request.command === "explain") {
      stream.markdown(
        [
          "**`/explain` (scaffold)**",
          "",
          "Next phase explains clauses via engine schema keys + your open document.",
          "",
          request.prompt ? `Topic: ${request.prompt}` : "Ask about a clause, e.g. `joker` / `must` chains.",
        ].join("\n"),
      );
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
