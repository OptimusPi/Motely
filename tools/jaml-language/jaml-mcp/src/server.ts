#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { RESOURCE_URI_META_KEY, RESOURCE_MIME_TYPE } from "@modelcontextprotocol/ext-apps";
import { z } from "zod";
import { parse as parseYaml } from "yaml";
import { readFileSync, readdirSync, existsSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import {
  JAML_ROOT_KEYS,
  CLAUSE_KEYS,
  looksLikeJson,
  unknownRootKeys,
} from "@motely/jaml-language-core";

// ── WASM engine (lazy-loaded) ──────────────────────────────────────────────
// motely-wasm v7.0.0: NativeAOT-LLVM, works in Node/Bun/Deno/browser
let wasmBooted = false;

async function bootWasm(): Promise<void> {
  if (wasmBooted) return;
  const wasm = await import("motely-wasm");
  await wasm.default.boot();
  wasmBooted = true;
}

// ── Paths ──────────────────────────────────────────────────────────────────
const HERE = typeof __dirname !== "undefined"
  ? __dirname
  : dirname(fileURLToPath(import.meta.url));

function findSchema(): any {
  const candidates = [
    resolve(HERE, "jaml.schema.json"),
    resolve(HERE, "..", "jaml.schema.json"),
    resolve(HERE, "..", "..", "..", "..", "jaml.schema.json"),
  ];
  for (const p of candidates) {
    try { return JSON.parse(readFileSync(p, "utf8")); } catch {}
  }
  return null;
}

function findExamplesDir(): string | null {
  const candidates = [
    resolve(HERE, "..", "examples"),
    resolve(HERE, "..", "..", "vscode-extension", "examples"),
    resolve(HERE, "..", "..", "..", "..", "JamlFilters"),
  ];
  for (const p of candidates) {
    if (existsSync(p)) return p;
  }
  return null;
}

// ── Schema data ────────────────────────────────────────────────────────────
const schema = findSchema();
const VALUE_MAP = new Map<string, string[]>();

if (schema) {
  const props = schema.properties ?? {};
  for (const [key, def] of Object.entries<any>(props)) {
    if (def.enum) VALUE_MAP.set(key, def.enum);
  }
  const clauseDefs =
    props.must?.items?.properties ??
    props.should?.items?.properties ?? {};
  for (const [key, def] of Object.entries<any>(clauseDefs)) {
    if (def.enum && !VALUE_MAP.has(key)) VALUE_MAP.set(key, def.enum);
    if (def.items?.enum && !VALUE_MAP.has(key)) VALUE_MAP.set(key, def.items.enum);
  }
  if (props.aesthetics?.items?.enum) {
    VALUE_MAP.set("aesthetics", props.aesthetics.items.enum);
  }
}

// ── Validation logic (shared with LSP) ─────────────────────────────────────
interface Diagnostic {
  severity: "error" | "warning" | "info";
  message: string;
  line: number;
}

function validateJaml(text: string): { valid: boolean; diagnostics: Diagnostic[] } {
  const diagnostics: Diagnostic[] = [];

  try {
    let root: unknown;
    if (looksLikeJson(text)) {
      root = JSON.parse(text);
    } else {
      root = parseYaml(text);
    }

    if (!root || typeof root !== "object" || Array.isArray(root)) {
      diagnostics.push({ severity: "error", message: "JAML root must be an object/mapping.", line: 1 });
      return { valid: false, diagnostics };
    }

    for (const bad of unknownRootKeys(root as Record<string, unknown>)) {
      diagnostics.push({ severity: "warning", message: `Unknown root key '${bad}'.`, line: 1 });
    }

    // Validate enum values
    const obj = root as Record<string, unknown>;
    for (const [key, values] of VALUE_MAP) {
      if (key in obj) {
        const val = obj[key];
        if (typeof val === "string" && !values.includes(val)) {
          diagnostics.push({
            severity: "warning",
            message: `Invalid value '${val}' for '${key}'. Valid: ${values.slice(0, 5).join(", ")}${values.length > 5 ? "..." : ""}`,
            line: 1,
          });
        }
      }
    }
  } catch (error) {
    diagnostics.push({ severity: "error", message: `Parse error: ${(error as Error).message}`, line: 1 });
  }

  return {
    valid: diagnostics.every((d) => d.severity !== "error"),
    diagnostics,
  };
}

// ── MCP Server ─────────────────────────────────────────────────────────────
const server = new McpServer({
  name: "jaml-mcp",
  version: "0.1.0",
});

// Tool: validate_jaml
server.tool(
  "validate_jaml",
  "Parse and validate a JAML filter. Returns diagnostics (errors/warnings) and whether the filter is valid.",
  { jaml: z.string().describe("JAML filter text (YAML or JSON)") },
  async ({ jaml }) => {
    const result = validateJaml(jaml);
    return {
      content: [{
        type: "text" as const,
        text: JSON.stringify(result, null, 2),
      }],
    };
  },
);

// Tool: compile_jummy
server.tool(
  "compile_jummy",
  "Compile Jummy text into JAML. Jummy is a human-friendly alternative syntax (supports mumble lines like 'Eternal Blueprint in Ante 1' and what/where blocks). Requires the Motely WASM engine.",
  { jummy: z.string().describe("Jummy source text to compile") },
  async ({ jummy }) => {
    try {
      await bootWasm();
      // eslint-disable-next-line @typescript-eslint/no-require-imports -- namespace re-export not resolved by tsc with NodeNext
      const wasm: any = await import("motely-wasm");
      const config = wasm.MotelyWasmHost.compileJummy(jummy);
      return {
        content: [{
          type: "text" as const,
          text: JSON.stringify({ success: true, config }, null, 2),
        }],
      };
    } catch (err) {
      return {
        content: [{
          type: "text" as const,
          text: JSON.stringify({ success: false, error: (err as Error).message }, null, 2),
        }],
        isError: true,
      };
    }
  },
);

// Tool: get_completions
server.tool(
  "get_completions",
  "Get valid JAML keys and enum values for autocompletion. Optionally filter by a specific key to get its valid values.",
  {
    key: z.string().optional().describe("Specific JAML key to get values for (e.g. 'joker', 'deck', 'boss'). Omit to get all keys."),
  },
  async ({ key }) => {
    if (key) {
      const values = VALUE_MAP.get(key);
      if (values) {
        return {
          content: [{
            type: "text" as const,
            text: JSON.stringify({ key, values }, null, 2),
          }],
        };
      }
      return {
        content: [{
          type: "text" as const,
          text: JSON.stringify({ error: `Unknown key '${key}'. Valid keys: ${[...VALUE_MAP.keys()].join(", ")}` }),
        }],
      };
    }

    return {
      content: [{
        type: "text" as const,
        text: JSON.stringify({
          rootKeys: [...JAML_ROOT_KEYS],
          clauseKeys: [...CLAUSE_KEYS],
          enumFields: Object.fromEntries(
            [...VALUE_MAP.entries()].map(([k, v]) => [k, { count: v.length, sample: v.slice(0, 5) }])
          ),
        }, null, 2),
      }],
    };
  },
);

// Tool: create_jaml
server.tool(
  "create_jaml",
  "Generate a JAML filter from a natural-language description. Returns valid JAML YAML text.",
  {
    description: z.string().describe("What the filter should search for, e.g. 'Find seeds with Blueprint and Brainstorm in ante 1 on Red Deck'"),
    deck: z.string().optional().describe("Deck to use (e.g. 'Red Deck')"),
    stake: z.string().optional().describe("Stake to use (e.g. 'White Stake')"),
  },
  async ({ description, deck, stake }) => {
    // Build a starter JAML from the description + schema knowledge
    const lines: string[] = [];
    lines.push(`# Generated from: ${description}`);
    lines.push(`name: "${description.slice(0, 60)}"`);
    if (deck) lines.push(`deck: ${deck}`);
    if (stake) lines.push(`stake: ${stake}`);
    lines.push("must:");

    // Extract joker names from description by matching against known values
    const jokerValues = VALUE_MAP.get("joker") ?? [];
    const mentioned = jokerValues.filter((j) => {
      const normalized = j.toLowerCase().replace(/[_-]/g, " ");
      return description.toLowerCase().includes(normalized);
    });

    if (mentioned.length > 0) {
      for (const j of mentioned) {
        lines.push(`  - joker: ${j}`);
      }
    } else {
      lines.push("  - joker: # add joker name here");
    }

    const jaml = lines.join("\n");
    const validation = validateJaml(jaml);

    return {
      content: [{
        type: "text" as const,
        text: JSON.stringify({ jaml, validation }, null, 2),
      }],
    };
  },
);

// Resource: schema
if (schema) {
  server.resource(
    "jaml-schema",
    "jaml://schema",
    { description: "The full JAML JSON Schema — defines all valid keys, types, and enum values for JAML filters.", mimeType: "application/json" },
    async () => ({
      contents: [{
        uri: "jaml://schema",
        mimeType: "application/json" as const,
        text: JSON.stringify(schema, null, 2),
      }],
    }),
  );
}

// Resource: example filters
const examplesDir = findExamplesDir();
if (examplesDir) {
  const jamlFiles = readdirSync(examplesDir).filter((f) => f.endsWith(".jaml"));
  for (const file of jamlFiles) {
    const name = file.replace(/\.jaml$/, "");
    server.resource(
      `example-${name}`,
      `jaml://examples/${file}`,
      { description: `Example JAML filter: ${name}`, mimeType: "text/yaml" },
      async () => ({
        contents: [{
          uri: `jaml://examples/${file}`,
          mimeType: "text/yaml" as const,
          text: readFileSync(resolve(examplesDir, file), "utf8"),
        }],
      }),
    );
  }
}

// ── MCP App: interactive search UI ─────────────────────────────────────────
const appHtmlPath = resolve(HERE, "app", "view.html");
let appHtml: string | null = null;
try { appHtml = readFileSync(appHtmlPath, "utf8"); } catch {}

if (appHtml) {
  server.resource(
    "jaml-search-app",
    "ui://jaml-mcp/search",
    { description: "Interactive JAML search UI — renders inline in Claude/VS Code", mimeType: RESOURCE_MIME_TYPE },
    async () => ({
      contents: [{
        uri: "ui://jaml-mcp/search",
        mimeType: RESOURCE_MIME_TYPE,
        text: appHtml!,
      }],
    }),
  );

  server.tool(
    "jaml_search_app",
    "Open the interactive JAML search app. Displays a rich UI where users can write filters, run searches, and see results in real time.",
    {
      jaml: z.string().optional().describe("Optional JAML filter to pre-fill in the editor"),
    },
    async ({ jaml }) => ({
      content: [{
        type: "text" as const,
        text: jaml
          ? `Opening JAML search app with pre-filled filter:\n\`\`\`yaml\n${jaml}\n\`\`\``
          : "Opening JAML search app.",
      }],
      _meta: {
        [RESOURCE_URI_META_KEY]: "ui://jaml-mcp/search",
      },
    }),
  );
}

// ── Start ──────────────────────────────────────────────────────────────────
const transport = new StdioServerTransport();
await server.connect(transport);
