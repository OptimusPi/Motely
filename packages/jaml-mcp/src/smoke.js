// End-to-end smoke test: spawn the stdio server, exercise every tool, the
// prompt, and the MCP App resource through a real MCP client, and assert the
// output. The validate_jaml and explain_jaml checks boot the real motely-wasm
// engine.
// Run with: npm run smoke

import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

import { IDE_RESOURCE_URI } from "./app.js";

const here = dirname(fileURLToPath(import.meta.url));

function assert(cond, msg) {
  if (!cond) throw new Error(`smoke: ${msg}`);
}

const SAMPLE =
  "Magic deck on White stake. Must have a Negative Perkeo from a soul card in " +
  "ante 1's first booster pack. Bonus points for an early Blueprint.";

const VALID_JAML = "must:\n  - joker: Blueprint\n    antes: [1]\n";
const INVALID_JAML = "must:\n  - notAClause: Nope\n";

const transport = new StdioClientTransport({
  command: process.execPath,
  args: [join(here, "server.js")],
});
const client = new Client({ name: "jaml-mcp-smoke", version: "0.0.0" });

await client.connect(transport);

// --- tools are all registered ---
const { tools } = await client.listTools();
const toolNames = tools.map((t) => t.name).sort();
for (const expected of [
  "nl_to_jaml",
  "jaml_reference",
  "jaml_examples",
  "validate_jaml",
  "explain_jaml",
  "open_jaml_ide",
]) {
  assert(toolNames.includes(expected), `missing tool ${expected}; got ${toolNames.join(", ")}`);
}

// --- nl_to_jaml: primes with guide + request ---
const nl = await client.callTool({
  name: "nl_to_jaml",
  arguments: { request: SAMPLE, deck: "Magic", stake: "White" },
});
const nlText = nl.content?.[0]?.text ?? "";
assert(nlText.includes("JAML authoring guide"), "nl_to_jaml missing the guide header");
assert(nlText.includes("MotelyJoker"), "nl_to_jaml missing enum vocabularies");
assert(nlText.includes(SAMPLE), "nl_to_jaml did not echo the request");
assert(nlText.includes("```yaml code block"), "nl_to_jaml missing the output contract");
assert(nlText.includes("validate_jaml"), "nl_to_jaml missing the validate step");
assert(nlText.includes("deck should be: Magic"), "nl_to_jaml missing the deck hint");

// --- jaml_reference: whole guide, and a single section ---
const refAll = await client.callTool({ name: "jaml_reference", arguments: {} });
assert(
  (refAll.content?.[0]?.text ?? "").includes("JAML authoring guide"),
  "jaml_reference (all) missing the guide",
);
const refSection = await client.callTool({
  name: "jaml_reference",
  arguments: { section: "document-shape" },
});
const refSectionText = refSection.content?.[0]?.text ?? "";
assert(refSectionText.includes("Document shape"), "jaml_reference section missing its heading");
assert(
  refSectionText.length < (refAll.content?.[0]?.text ?? "").length,
  "jaml_reference section was not narrower than the whole guide",
);
const refBad = await client.callTool({
  name: "jaml_reference",
  arguments: { section: "nope" },
});
assert(refBad.isError, "jaml_reference should error on an unknown section");

// --- jaml_examples: all, and filtered ---
const exAll = await client.callTool({ name: "jaml_examples", arguments: {} });
assert(
  (exAll.content?.[0]?.text ?? "").includes("```yaml"),
  "jaml_examples (all) returned no examples",
);
const exFiltered = await client.callTool({
  name: "jaml_examples",
  arguments: { query: "mustNot" },
});
assert(
  (exFiltered.content?.[0]?.text ?? "").includes("mustNot"),
  "jaml_examples filter did not match the mustNot example",
);

// --- validate_jaml: real engine, valid and invalid ---
const vGood = await client.callTool({ name: "validate_jaml", arguments: { jaml: VALID_JAML } });
assert(
  (vGood.content?.[0]?.text ?? "") === "valid" && !vGood.isError,
  `validate_jaml(valid) expected "valid", got ${JSON.stringify(vGood.content?.[0]?.text)}`,
);
const vBad = await client.callTool({ name: "validate_jaml", arguments: { jaml: INVALID_JAML } });
assert(
  vBad.isError && (vBad.content?.[0]?.text ?? "").startsWith("invalid:"),
  `validate_jaml(invalid) expected an error, got ${JSON.stringify(vBad.content?.[0]?.text)}`,
);

// --- explain_jaml: real engine, valid and invalid ---
const eGood = await client.callTool({ name: "explain_jaml", arguments: { jaml: VALID_JAML } });
assert(
  !eGood.isError && (eGood.content?.[0]?.text ?? "").includes("eval plan"),
  `explain_jaml(valid) missing the eval plan, got ${JSON.stringify(eGood.content?.[0]?.text)}`,
);
const eBad = await client.callTool({ name: "explain_jaml", arguments: { jaml: INVALID_JAML } });
assert(
  eBad.isError && (eBad.content?.[0]?.text ?? "").startsWith("invalid:"),
  `explain_jaml(invalid) expected an error, got ${JSON.stringify(eBad.content?.[0]?.text)}`,
);

// --- open_jaml_ide: an MCP App (SEP-1865) — tool links to a ui:// resource ---
const ideTool = tools.find((t) => t.name === "open_jaml_ide");
const ideToolMeta = ideTool?._meta ?? {};
const ideResourceUri = ideToolMeta.ui?.resourceUri ?? ideToolMeta["ui/resourceUri"];
assert(
  ideResourceUri === IDE_RESOURCE_URI,
  `open_jaml_ide tool should link to ${IDE_RESOURCE_URI} via _meta.ui, got ${ideResourceUri}`,
);

// The tool result carries the seed JAML as structuredContent — the host
// forwards it to the iframe; the HTML is not in the tool result.
const ideSeed = await client.callTool({
  name: "open_jaml_ide",
  arguments: { jaml: VALID_JAML },
});
assert(
  ideSeed.structuredContent?.jaml === VALID_JAML,
  "open_jaml_ide did not return the supplied JAML as structuredContent",
);
assert(
  ideSeed.content?.[0]?.text?.includes("Blueprint"),
  "open_jaml_ide text content should echo the seeded JAML for the model",
);
const ideDefault = await client.callTool({ name: "open_jaml_ide", arguments: {} });
assert(
  ideDefault.structuredContent?.jaml?.includes("deck: Magic"),
  "open_jaml_ide with no seed did not fall back to the starter document",
);

// The ui:// resource is registered and serves the MCP App HTML.
const { resources } = await client.listResources();
assert(
  resources.some((r) => r.uri === IDE_RESOURCE_URI),
  `expected resource ${IDE_RESOURCE_URI}; got ${resources.map((r) => r.uri).join(", ")}`,
);
const ideResource = await client.readResource({ uri: IDE_RESOURCE_URI });
const ideContent = ideResource.contents?.[0];
assert(
  ideContent?.mimeType === "text/html;profile=mcp-app",
  `IDE resource mimeType should be text/html;profile=mcp-app, got ${ideContent?.mimeType}`,
);
const ideHtml = ideContent?.text ?? "";
assert(ideHtml.includes("esm.sh/jaml-ui@"), "IDE HTML missing the jaml-ui CDN import");
assert(ideHtml.includes("JamlIde"), "IDE HTML does not mount JamlIde");
assert(
  ideHtml.includes("@modelcontextprotocol/ext-apps"),
  "IDE HTML does not load the MCP Apps client SDK",
);
assert(
  ideContent?._meta?.ui?.csp?.resourceDomains?.includes("https://esm.sh"),
  "IDE resource must declare esm.sh in its CSP resourceDomains or the host blocks the CDN",
);

// --- prompt ---
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
assert(promptText.includes(SAMPLE), "prompt did not echo the request");
assert(promptText.includes("JAML authoring guide"), "prompt missing the guide");

await client.close();
console.log(
  "smoke: OK — 6 tools + prompt + MCP App resource verified (validate/explain ran the real engine)",
);
