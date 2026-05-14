// End-to-end smoke test: spawn the stdio server, exercise the tool and prompt
// through a real MCP client, and assert the primed output is well formed.
// Run with: npm run smoke

import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));

function assert(cond, msg) {
  if (!cond) throw new Error(`smoke: ${msg}`);
}

const SAMPLE =
  "Magic deck on White stake. Must have a Negative Perkeo from a soul card in " +
  "ante 1's first booster pack. Bonus points for an early Blueprint.";

const transport = new StdioClientTransport({
  command: process.execPath,
  args: [join(here, "server.js")],
});
const client = new Client({ name: "jaml-mcp-smoke", version: "0.0.0" });

await client.connect(transport);

const { tools } = await client.listTools();
assert(
  tools.some((t) => t.name === "nl_to_jaml"),
  `expected tool nl_to_jaml, got: ${tools.map((t) => t.name).join(", ")}`,
);

const toolResult = await client.callTool({
  name: "nl_to_jaml",
  arguments: { request: SAMPLE, deck: "Magic", stake: "White" },
});
const text = toolResult.content?.[0]?.text ?? "";
assert(text.includes("JAML authoring guide"), "tool output missing the guide header");
assert(text.includes("MotelyJoker"), "tool output missing enum vocabularies");
assert(text.includes("Perkeo"), "tool output missing a known enum value");
assert(text.includes(SAMPLE), "tool output did not echo the request");
assert(text.includes("```yaml code block"), "tool output missing the output contract");
assert(text.includes("deck should be: Magic"), "tool output missing the deck hint");

const { prompts } = await client.listPrompts();
assert(
  prompts.some((p) => p.name === "nl_to_jaml"),
  `expected prompt nl_to_jaml, got: ${prompts.map((p) => p.name).join(", ")}`,
);

const promptResult = await client.getPrompt({
  name: "nl_to_jaml",
  arguments: { request: SAMPLE },
});
const promptText = promptResult.messages?.[0]?.content?.text ?? "";
assert(promptText.includes(SAMPLE), "prompt output did not echo the request");
assert(promptText.includes("JAML authoring guide"), "prompt output missing the guide");

await client.close();
console.log("smoke: OK — tool + prompt both prime correctly");
