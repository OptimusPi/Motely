/**
 * @jimbo chat participant.
 *
 * Owns the Copilot Chat turn when the user @-mentions jimbo.
 * Slash paths call Motely directly; freeform path runs LM + Motely tools (J4).
 *
 * https://code.visualstudio.com/api/extension-guides/ai/chat
 * https://code.visualstudio.com/api/extension-guides/ai/tools
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

/** Registered Motely LM tools (must match package.json languageModelTools.name). */
export const MOTELY_TOOL_NAMES = [
  "motely_validate_jaml",
  "motely_search_seeds",
  "motely_explain_jaml",
] as const;

const MAX_TOOL_ROUNDS = 6;

const SYSTEM = [
  "You are Jimbo — Motely/JAML assistant for Balatro seed filters.",
  "One grammar: Motely engine only. Never invent seeds or clause keys.",
  "Use tools for engine facts: motely_validate_jaml before motely_search_seeds;",
  "motely_explain_jaml for schema/vocab. Prefer absolute filePath of the open .jaml when known.",
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
        const input = resolveSlashJamlInput(request.prompt, /*allowPromptAsJaml*/ true);
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
        const editor = activeJamlInput();
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
          : editor;
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

    // J4: freeform LM + Motely tools (validate / search / explain)
    const prompt = request.prompt?.trim() || "ping";
    if (request.model) {
      try {
        await runFreeformWithTools(request, chatContext, stream, token, prompt);
        return { metadata: { command: "chat" } };
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        stream.markdown(`_Model / tool loop failed:_ ${msg}\n\n`);
      }
    }

    stream.markdown(
      [
        `**Jimbo online** (no language model on this request).`,
        "",
        `| | |`,
        `|--|--|`,
        `| you said | \`${escapePipes(prompt)}\` |`,
        `| slash | \`${slash}\` |`,
        `| model | none (install Copilot Chat for freeform + tools) |`,
        "",
        "Slashes (engine, no model): `@jimbo /find` · `/validate` · `/explain`",
        "Tools: `#validateJaml` · `#findSeeds` · `#explainJaml`",
      ].join("\n"),
    );
    return { metadata: { command: "pong" } };
  };

  const participant = vscode.chat.createChatParticipant(JIMBO_PARTICIPANT_ID, handler);
  participant.iconPath = vscode.Uri.joinPath(context.extensionUri, "media", "jimbo.svg");

  participant.followupProvider = {
    provideFollowups(result) {
      const cmd = (result.metadata as { command?: string } | undefined)?.command;
      if (cmd === "find") {
        return [
          { prompt: "/validate", label: "Validate this filter" },
          { prompt: "/find 3", label: "Find three seeds" },
          { prompt: "/explain must", label: "Explain must" },
        ];
      }
      if (cmd === "validate") {
        return [
          { prompt: "/find", label: "Find one seed" },
          { prompt: "/explain must", label: "Explain must" },
        ];
      }
      if (cmd === "explain") {
        return [
          { prompt: "/validate", label: "Validate JAML" },
          { prompt: "/find", label: "Find seeds" },
        ];
      }
      return [
        { prompt: "/validate", label: "Validate JAML" },
        { prompt: "/find", label: "Find seeds" },
        { prompt: "/explain joker", label: "Explain joker clause" },
      ];
    },
  };

  context.subscriptions.push(participant);
  return participant;
}

/**
 * J4 freeform loop: stream LM text, invoke Motely tools via lm.invokeTool, re-prompt until done.
 * Uses request.toolInvocationToken so confirmation UI attaches to the chat turn.
 */
async function runFreeformWithTools(
  request: vscode.ChatRequest,
  chatContext: vscode.ChatContext,
  stream: vscode.ChatResponseStream,
  token: vscode.CancellationToken,
  prompt: string,
): Promise<void> {
  const model = request.model;
  const motelyTools = vscode.lm.tools.filter((t) =>
    (MOTELY_TOOL_NAMES as readonly string[]).includes(t.name),
  );

  const openHint = activeJamlHint();
  const systemWithContext = openHint
    ? `${SYSTEM}\nOpen JAML context: ${openHint}`
    : SYSTEM;

  const messages: vscode.LanguageModelChatMessage[] = [
    vscode.LanguageModelChatMessage.User(systemWithContext),
    ...historyAsMessages(chatContext),
    vscode.LanguageModelChatMessage.User(prompt),
  ];

  const toolReferences = [...request.toolReferences];
  let rounds = 0;

  while (rounds < MAX_TOOL_ROUNDS) {
    if (token.isCancellationRequested) {
      return;
    }

    const options: vscode.LanguageModelChatRequestOptions = {
      justification: "Jimbo answers JAML / Motely / Balatro seed questions with real engine tools.",
    };

    const forced = toolReferences.shift();
    if (forced) {
      options.toolMode = vscode.LanguageModelChatToolMode.Required;
      options.tools = motelyTools
        .filter((t) => t.name === forced.name)
        .map(toChatTool);
      if (options.tools.length === 0) {
        // Unknown #tool — fall back to full Motely set
        options.toolMode = vscode.LanguageModelChatToolMode.Auto;
        options.tools = motelyTools.map(toChatTool);
      }
    } else {
      options.toolMode = vscode.LanguageModelChatToolMode.Auto;
      options.tools = motelyTools.map(toChatTool);
    }

    stream.progress(
      rounds === 0
        ? `Jimbo + ${options.tools?.length ?? 0} Motely tools…`
        : `Tool round ${rounds + 1}…`,
    );

    const response = await model.sendRequest(messages, options, token);
    const toolCalls: vscode.LanguageModelToolCallPart[] = [];
    let assistantText = "";

    for await (const part of response.stream) {
      if (token.isCancellationRequested) {
        return;
      }
      if (part instanceof vscode.LanguageModelTextPart) {
        stream.markdown(part.value);
        assistantText += part.value;
      } else if (part instanceof vscode.LanguageModelToolCallPart) {
        toolCalls.push(part);
      }
    }

    if (toolCalls.length === 0) {
      return;
    }

    // Assistant turn: tool call parts (text already streamed to user)
    const assistantParts: Array<vscode.LanguageModelTextPart | vscode.LanguageModelToolCallPart> =
      [];
    if (assistantText) {
      assistantParts.push(new vscode.LanguageModelTextPart(assistantText));
    }
    for (const call of toolCalls) {
      assistantParts.push(call);
      stream.progress(`Calling \`${call.name}\`…`);
    }
    messages.push(vscode.LanguageModelChatMessage.Assistant(assistantParts));

    // User turn: tool results
    const resultParts: vscode.LanguageModelToolResultPart[] = [];
    for (const call of toolCalls) {
      const result = await invokeMotelyTool(call, request, token);
      resultParts.push(
        new vscode.LanguageModelToolResultPart(call.callId, result.content),
      );
    }
    messages.push(vscode.LanguageModelChatMessage.User(resultParts));

    rounds++;
  }

  stream.markdown(
    `\n\n_Stopped after ${MAX_TOOL_ROUNDS} tool rounds. Use \`/find\` / \`/validate\` for a direct engine call._`,
  );
}

async function invokeMotelyTool(
  call: vscode.LanguageModelToolCallPart,
  request: vscode.ChatRequest,
  token: vscode.CancellationToken,
): Promise<vscode.LanguageModelToolResult> {
  try {
    return await vscode.lm.invokeTool(
      call.name,
      {
        input: (call.input ?? {}) as object,
        toolInvocationToken: request.toolInvocationToken,
      },
      token,
    );
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    return new vscode.LanguageModelToolResult([
      new vscode.LanguageModelTextPart(`Tool \`${call.name}\` failed: ${msg}`),
    ]);
  }
}

function toChatTool(t: vscode.LanguageModelToolInformation): vscode.LanguageModelChatTool {
  return {
    name: t.name,
    description: t.description,
    inputSchema: t.inputSchema,
  };
}

function activeJamlInput():
  | { filePath: string; label: string }
  | { jamlText: string; label: string }
  | undefined {
  const editor = vscode.window.activeTextEditor;
  if (
    !editor ||
    !(editor.document.languageId === "jaml" || editor.document.fileName.endsWith(".jaml"))
  ) {
    return undefined;
  }
  if (editor.document.uri.scheme === "file") {
    return { filePath: editor.document.uri.fsPath, label: editor.document.uri.fsPath };
  }
  return { jamlText: editor.document.getText(), label: editor.document.uri.toString() };
}

function activeJamlHint(): string | undefined {
  const input = activeJamlInput();
  if (!input) {
    return undefined;
  }
  if ("filePath" in input) {
    return `filePath=${input.filePath} (prefer this absolute path for tools)`;
  }
  return `untitled buffer (${input.label}); pass jamlText from editor if tools need it`;
}

function resolveSlashJamlInput(
  prompt: string | undefined,
  allowPromptAsJaml: boolean,
): { jamlText?: string; filePath?: string; label: string } | undefined {
  const trimmed = prompt?.trim() ?? "";
  if (allowPromptAsJaml && trimmed) {
    return { jamlText: trimmed, label: "(chat prompt as JAML)" };
  }
  return activeJamlInput();
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
  return out.slice(-8);
}

function escapePipes(s: string): string {
  return s.replace(/\|/g, "\\|").replace(/\n/g, " ");
}
