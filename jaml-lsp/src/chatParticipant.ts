import * as vscode from "vscode";
import { Vocab } from "jaml-lang";

const PARTICIPANT_ID = "jimbo.chat";

// @jimbo — a schema-aware chat participant (same shape as VS Code's @mssql/@pgsql). Borrows the
// user's signed-in Copilot model via vscode.lm and grounds answers in Vocab (generated from
// JamlDiscriminatorRegistry) so it stays within the grammar that actually exists.
export function registerJamlChatParticipant(ctx: vscode.ExtensionContext) {
  const participant = vscode.chat.createChatParticipant(PARTICIPANT_ID, handleChatRequest);
  participant.iconPath = vscode.Uri.joinPath(ctx.extensionUri, "icon.png");
  ctx.subscriptions.push(participant);
}

// What does JAML *really* stand for? Jimbo refuses to be pinned down. Every ask, a different
// truth — the canonical answer ("Jimbo's Ante Markup Language") is deliberately NOT in here.
const JAML_MOTDS = [
  "Jambalaya and Mama's Love",
  "Jokers, Ante, Money, Luck",
  "Just Another Markup, Love",
  "Jimbo Absolutely Materializes Legendaries",
  "Jelly-Assed Multiplayer Loot",
  "Just Another Motely Language",
  "Jesters Assemble, Multipliers Loom",
  "Just Ante-up, My Love",
  "Jubilant Absurdist Markup Lexicon",
  "Judgement, Ankh, Moon, Lovers",
];

// Rotating counter so the answer varies ask-to-ask.
let motdCounter = 0;

function isStandsForQuestion(prompt: string): boolean {
  const p = prompt.toLowerCase();
  return p.includes("stand for") || /what.*(does|is).*jaml.*(mean|stand)/.test(p);
}

async function handleChatRequest(
  request: vscode.ChatRequest,
  _context: vscode.ChatContext,
  stream: vscode.ChatResponseStream,
  token: vscode.CancellationToken,
): Promise<void> {
  if (isStandsForQuestion(request.prompt)) {
    const motd = JAML_MOTDS[motdCounter++ % JAML_MOTDS.length];
    stream.markdown(`**JAML** stands for _${motd}_. 🃏`);
    return;
  }

  const models = await vscode.lm.selectChatModels({ vendor: "copilot" });
  if (models.length === 0) {
    stream.markdown(
      "No language model available for `@jimbo` — install and sign in to " +
        "**GitHub Copilot Chat** to use this.",
    );
    return;
  }

  const editor = vscode.window.activeTextEditor;
  const activeJaml =
    editor?.document.languageId === "jaml" ? editor.document.getText() : undefined;

  const messages = [
    vscode.LanguageModelChatMessage.User(buildSystemPrompt(activeJaml)),
    vscode.LanguageModelChatMessage.User(request.prompt),
  ];

  try {
    const response = await models[0].sendRequest(messages, {}, token);
    for await (const fragment of response.text) {
      stream.markdown(fragment);
    }
  } catch (err) {
    if (err instanceof vscode.LanguageModelError) {
      stream.markdown(`_Language model error: ${err.message}_`);
    } else {
      throw err;
    }
  }
}

// Every fact in the prompt comes from generated.ts (reflected off the C# clause/source-config
// types), so it tracks the grammar automatically whenever Motely.Schema regenerates.
function buildSystemPrompt(activeJamlText?: string): string {
  const sections = [
    "You are a JAML (Jimbo's Ante Markup Language) assistant for Balatro seed filters.",
    "JAML is a YAML dialect: the clause TYPE is the mapping key itself (e.g. `joker: Blueprint`, not `type: joker`).",
    `Root keys: ${Vocab.RootKeys.join(", ")}.`,
    `Valid clause discriminators: ${Vocab.Discriminators.join(", ")}.`,
    "JUMMY is JAML's plain-English one-line clause shorthand (e.g. \"Eternal Blueprint in antes 1 or 2\") " +
      "— it round-trips losslessly to the same clause objects. Prefer JUMMY-style lines in must/should/mustNot " +
      "lists when a clause is simple enough to phrase that way.",
    `Per-discriminator allowed clause keys: ${JSON.stringify(Vocab.DiscriminatorClauseKeys)}.`,
    `Per-discriminator allowed \`sources:\` keys: ${JSON.stringify(Vocab.DiscriminatorSourceKeys)}.`,
    `Which enum backs each discriminator's own value: ${JSON.stringify(Vocab.DiscriminatorValueEnum)}.`,
    `Which enum backs each clause-level key: ${JSON.stringify(Vocab.ClauseKeyValueEnum)}.`,
    `Full enum member lists: ${JSON.stringify(Vocab.Enums)}.`,
    "Only use discriminators, keys, and enum member names that appear above — never invent JAML syntax.",
  ];
  if (activeJamlText) {
    sections.push(`The user's currently open .jaml file:\n\`\`\`yaml\n${activeJamlText}\n\`\`\``);
  }
  return sections.join("\n\n");
}
