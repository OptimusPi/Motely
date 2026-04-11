#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { RESOURCE_URI_META_KEY, RESOURCE_MIME_TYPE } from "@modelcontextprotocol/ext-apps";
import { z } from "zod";
import { readFileSync, readdirSync, existsSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import {
  JAML_ROOT_KEYS,
  CLAUSE_KEYS,
} from "@motely/jaml-language-core";

import * as motely from "motely-wasm-compat";

// ── WASM engine (direct Bootsharp contract) ────────────────────────────────
let bootPromise: Promise<void> | null = null;
type SearchSession = { cancel(): void };
let currentSession: SearchSession | null = null;
const bootsharp = motely.default;
const { Motely, MotelyWasmHost, SearchEvents } = motely;
type MotelyWasmHostExtended = typeof MotelyWasmHost & {
  singleGetBossForAnte(
    seed: string,
    deck: number,
    stake: number,
    ante: number
  ): number;
  singleGetNextTag(
    seed: string,
    deck: number,
    stake: number,
    ante: number
  ): number;
  singleGetAnteFirstVoucher(
    seed: string,
    deck: number,
    stake: number,
    ante: number
  ): number;
  singleGetNextShopItem(
    seed: string,
    deck: number,
    stake: number,
    ante: number
  ): unknown;
  startRandomSearchFromJaml(jaml: string, seedCount: number): SearchSession;
};
const Host = MotelyWasmHost as unknown as MotelyWasmHostExtended;

async function ensureBooted(): Promise<void> {
  bootPromise ??= bootsharp.boot().then(() => {}).catch((err: unknown) => {
    bootPromise = null;
    throw err;
  });
  await bootPromise;
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
    try { return JSON.parse(readFileSync(p, "utf8")); } catch { }
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

const schema = findSchema();

// ── MCP Server ─────────────────────────────────────────────────────────────
const server = new McpServer({
  name: "jaml-mcp",
  version: "0.1.0",
});

// Tool: get_version
server.tool(
  "get_version",
  "Get the Motely WASM engine version.",
  {},
  async () => {
    try {
      await ensureBooted();
      return { content: [{ type: "text" as const, text: MotelyWasmHost.getVersion() }] };
    } catch (err) {
      return { content: [{ type: "text" as const, text: (err as Error).message }], isError: true };
    }
  },
);

// Tool: validate_jaml — uses the real WASM loadJaml, not JS approximation
server.tool(
  "validate_jaml",
  "Parse and validate a JAML filter through the Motely WASM engine (MotelyWasmHost.loadJaml). After YAML parse, runs the same search-plan checks as a real search (JamlSearchBuilder.EnsureRunnablePlan / CreatePlan): impossible soul-joker booster slots, etc. Returns the loaded JamlConfig on success or the exact C# error on failure.",
  { jaml: z.string().describe("JAML filter text (YAML or JSON)") },
  async ({ jaml }) => {
    try {
      await ensureBooted();
      const config = MotelyWasmHost.loadJaml(jaml);
      return {
        content: [{
          type: "text" as const,
          text: JSON.stringify({ valid: true, config }, null, 2),
        }],
      };
    } catch (err) {
      return {
        content: [{
          type: "text" as const,
          text: JSON.stringify({ valid: false, error: (err as Error).message }, null, 2),
        }],
      };
    }
  },
);

// Tool: compile_jummy — Jummy → JamlConfig via WASM
server.tool(
  "compile_jummy",
  "Compile Jummy text into a JamlConfig via MotelyWasmHost.compileJummy. Jummy supports mumble lines ('Eternal Blueprint in Ante 1') and what/where blocks.",
  { jummy: z.string().describe("Jummy source text to compile") },
  async ({ jummy }) => {
    try {
      await ensureBooted();
      const config = MotelyWasmHost.compileJummy(jummy);
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

// Tool: inspect_seed — look up what a specific seed produces
server.tool(
  "inspect_seed",
  "Inspect a specific seed via MotelyWasmHost single-query APIs and report bosses, tags, vouchers, booster packs, and shop items per ante.",
  {
    seed: z.string().describe("The seed to inspect (e.g. 'ABC123')"),
    deck: z.string().describe("Deck name (e.g. 'Red')"),
    stake: z.string().describe("Stake name (e.g. 'White')"),
    antes: z.number().default(8).describe("Number of antes to inspect (default 8)"),
  },
  async ({ seed, deck, stake, antes }) => {
    try {
      await ensureBooted();
      const deckEnum = Motely.MotelyDeck[deck as keyof typeof Motely.MotelyDeck];
      const stakeEnum = Motely.MotelyStake[stake as keyof typeof Motely.MotelyStake];
      if (deckEnum === undefined) return { content: [{ type: "text" as const, text: `Unknown deck '${deck}'. Valid: ${Object.keys(Motely.MotelyDeck).filter(k => isNaN(Number(k))).join(", ")}` }], isError: true };
      if (stakeEnum === undefined) return { content: [{ type: "text" as const, text: `Unknown stake '${stake}'. Valid: ${Object.keys(Motely.MotelyStake).filter(k => isNaN(Number(k))).join(", ")}` }], isError: true };

      const result: Record<string, any> = { seed, deck, stake };

      for (let ante = 1; ante <= antes; ante++) {
        result[`ante${ante}`] = {
          boss: Host.singleGetBossForAnte(seed, deckEnum, stakeEnum, ante),
          tag: Host.singleGetNextTag(seed, deckEnum, stakeEnum, ante),
          voucher: Host.singleGetAnteFirstVoucher(seed, deckEnum, stakeEnum, ante),
          shopItem: Host.singleGetNextShopItem(seed, deckEnum, stakeEnum, ante),
        };
      }

      return {
        content: [{
          type: "text" as const,
          text: JSON.stringify(result, null, 2),
        }],
      };
    } catch (err) {
      return { content: [{ type: "text" as const, text: (err as Error).message }], isError: true };
    }
  },
);

// Tool: run_search — run a JAML search via WASM SearchEvents
server.tool(
  "run_search",
  "Run a seed search using a JAML filter. Boots WASM, loads the filter via loadJaml, runs startRandomSearch, and collects results via SearchEvents.",
  {
    jaml: z.string().describe("JAML filter text"),
    seed_count: z.number().default(100000).describe("Number of random seeds to search (default 100000)"),
  },
  async ({ jaml, seed_count }) => {
    try {
      if (currentSession) {
        return { content: [{ type: "text" as const, text: "A search is already running." }], isError: true };
      }

      await ensureBooted();

      return await new Promise((resolve, reject) => {
        const results: Array<{ seed: string; score: number; tally: Int32Array }> = [];

        const resultHandler = (seed: string, score: number, tally: Int32Array) => {
          results.push({ seed, score, tally });
        };

        let session: SearchSession | null = null;
        const cleanup = () => {
          SearchEvents.onResult.unsubscribe(resultHandler);
          SearchEvents.onComplete.unsubscribe(completeHandler);
          if (session && currentSession === session) currentSession = null;
        };

        const completeHandler = (status: string, searched: bigint, matching: bigint) => {
          cleanup();
          results.sort((a, b) => b.score - a.score);
          resolve({
            content: [{
              type: "text" as const,
              text: JSON.stringify({
                status,
                searched: searched.toString(),
                matching: matching.toString(),
                resultCount: results.length,
                results: results.slice(0, 200),
              }, null, 2),
            }],
          });
        };

        SearchEvents.onResult.subscribe(resultHandler);
        SearchEvents.onComplete.subscribe(completeHandler);

        try {
          session = Host.startRandomSearchFromJaml(jaml, seed_count);
          currentSession = session;
        } catch (err) {
          cleanup();
          reject(err);
        }
      });
    } catch (err) {
      return { content: [{ type: "text" as const, text: (err as Error).message }], isError: true };
    }
  },
);

// Tool: stop_search
server.tool(
  "stop_search",
  "Stop any currently running search.",
  {},
  async () => {
    try {
      if (currentSession) {
        const session = currentSession;
        currentSession = null;
        session.cancel();
        return { content: [{ type: "text" as const, text: "Search cancelled." }] };
      }
      return { content: [{ type: "text" as const, text: "No search is currently running." }] };
    } catch (err) {
      return { content: [{ type: "text" as const, text: (err as Error).message }], isError: true };
    }
  },
);

// Tool: get_completions — schema-driven (no WASM needed)
server.tool(
  "get_completions",
  "Get valid JAML root keys and clause keys for autocompletion.",
  {},
  async () => ({
    content: [{
      type: "text" as const,
      text: JSON.stringify({ rootKeys: [...JAML_ROOT_KEYS], clauseKeys: [...CLAUSE_KEYS] }, null, 2),
    }],
  }),
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
try { appHtml = readFileSync(appHtmlPath, "utf8"); } catch { }

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
