/**
 * MCP tool registration for the Balatro seed search server.
 *
 * The C# engine (motely-wasm-compat) owns JAML parsing, validation, casing
 * rules, and search execution. This file is intentionally a thin MCP shell:
 * it boots Bootsharp once at module load, registers four tools with the
 * SDK, and forwards inputs straight to the engine. Engine errors propagate
 * as `isError` MCP responses with the engine's own message verbatim.
 */
import { existsSync } from "node:fs";
import { readFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  registerAppResource,
  registerAppTool,
  RESOURCE_MIME_TYPE,
} from "@modelcontextprotocol/ext-apps/server";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import dotnet, { MotelyWasmHost, SearchEvents } from "motely-wasm-compat";
import { z } from "zod";

// Derive the single-seed context type from the actual function signature so
// this file compiles regardless of whether the generated TS exposes the type
// under the `Analysis` namespace (pre-9.0.1 layout) or at the top level
// (9.0.1+ JSPreferences strips the `Motely.Analysis.` prefix). See
// Motely.BrowserWasm/BootsharpInterop.cs.
type SingleSearchContext = ReturnType<typeof MotelyWasmHost.motelySingleSearchContext>;

const MAX_RANDOM_SEEDS = 1_000_000;
const DEFAULT_RANDOM_SEEDS = 1_000_000;
const MAX_RESULTS = 200;

/**
 * Boot Bootsharp once per process at module load.
 * Every async tool handler awaits this; it resolves immediately after the
 * first boot completes.
 */
export const bootPromise = dotnet.boot();

// ── MCP Apps UI bundle (bundled HTML for compliant hosts) ────────────────────

const SEARCH_UI_URI = "ui://balatro-seed-mcp/jaml-search-app.html";

async function loadSearchAppHtml(): Promise<string> {
  const here = dirname(fileURLToPath(import.meta.url));
  const candidates = [
    join(process.cwd(), "mcp-ui/dist/jaml-search-app.html"),
    join(here, "..", "mcp-ui", "dist", "jaml-search-app.html"),
    join(here, "..", "..", "mcp-ui", "dist", "jaml-search-app.html"),
  ];
  for (const p of candidates) {
    if (existsSync(p)) return readFile(p, "utf-8");
  }
  throw new Error(
    "MCP App HTML missing: run `pnpm run build:mcp-ui` before starting the server."
  );
}

// ── Search ───────────────────────────────────────────────────────────────────

interface SearchResult {
  seed: string;
  score: number;
  tally: number[];
}

export interface SearchResponse {
  status: string;
  seedsSearched: string;
  matchesFound: string;
  totalMatches: string;
  resultsShown: string;
  results: SearchResult[];
}

/**
 * Run a random seed search against a JAML filter.
 *
 * Subscribes to SearchEvents (`onResult`, `onComplete`) for the duration of
 * one search, kicks off the search via `MotelyWasmHost.startRandomSearchFromJaml`,
 * then resolves when `onComplete` fires. We deliberately do NOT call
 * `session.waitForCompletionAsync(...)` on the returned handle: the host owns
 * the search lifetime, JS just observes the event stream. This keeps the JS
 * side independent of any per-instance Bootsharp marshaling for
 * `IMotelySearchSession`, which historically has been fragile.
 */
export async function searchSeeds(
  jamlJson: string,
  seedCount: number = DEFAULT_RANDOM_SEEDS
): Promise<SearchResponse> {
  await bootPromise;

  return new Promise<SearchResponse>((resolve, reject) => {
    const results: SearchResult[] = [];

    const onResult = (seed: string, score: number, tally: Int32Array) => {
      results.push({ seed, score, tally: Array.from(tally) });
    };

    const onComplete = (
      status: string,
      seedsSearched: bigint,
      matchingSeeds: bigint,
    ) => {
      SearchEvents.onResult.unsubscribe(onResult);
      SearchEvents.onComplete.unsubscribe(onComplete);
      const sorted = results.sort((a, b) => b.score - a.score);
      const shown = sorted.slice(0, MAX_RESULTS);
      resolve({
        status,
        seedsSearched: seedsSearched.toString(),
        matchesFound: matchingSeeds.toString(),
        totalMatches: String(sorted.length),
        resultsShown: String(shown.length),
        results: shown,
      });
    };

    SearchEvents.onResult.subscribe(onResult);
    SearchEvents.onComplete.subscribe(onComplete);

    try {
      MotelyWasmHost.startRandomSearchFromJaml(jamlJson, seedCount);
    } catch (err) {
      SearchEvents.onResult.unsubscribe(onResult);
      SearchEvents.onComplete.unsubscribe(onComplete);
      reject(err);
    }
  });
}

// ── Single-seed analysis ─────────────────────────────────────────────────────

/**
 * Open a single-seed search context for the given seed using the deck/stake
 * declared in the supplied JAML filter (defaults to Red/White if unset by
 * the engine). Returned object exposes `getBossForAnte`, `getNextTag`,
 * `getNextShopItem`, etc. — see `IMotelySingleSearchContextImpl`.
 */
export async function analyzeSeed(
  seed: string,
  jamlJson: string
): Promise<SingleSearchContext> {
  await bootPromise;
  const config = MotelyWasmHost.loadJaml(jamlJson);
  return MotelyWasmHost.motelySingleSearchContext(seed, config.deck, config.stake);
}

// ── MCP tool registration ────────────────────────────────────────────────────

export function registerTools(server: McpServer) {
  registerAppResource(
    server,
    "JAML Search UI",
    SEARCH_UI_URI,
    {
      description:
        "Balatro seed search viewer (React + Vercel json-render) for MCP Apps hosts.",
    },
    async () => ({
      contents: [
        {
          uri: SEARCH_UI_URI,
          mimeType: RESOURCE_MIME_TYPE,
          text: await loadSearchAppHtml(),
        },
      ],
    })
  );

  // search_seeds ────────────────────────────────────────────────────────────
  registerAppTool(
    server,
    "search_seeds",
    {
      title: "Search Balatro seeds",
      description:
        "Search for Balatro seeds matching a JAML filter. Translate the user's request into JAML yourself; never ask the user to write JAML.\n\n" +
        'JAML shape: {"deck":"Red","stake":"White","must":[{"joker":"Blueprint","antes":[1]}]}\n\n' +
        "Decks: Red, Blue, Yellow, Green, Black, Magic, Nebula, Ghost, Abandoned, Checkered, Zodiac, Painted, Anaglyph, Plasma, Erratic.\n" +
        "Stakes: White, Red, Green, Black, Blue, Purple, Orange, Gold.\n" +
        "Clauses: joker, voucher, boss, tag, tarotCard, spectralCard, planetCard, standardCard, erraticCard.\n" +
        "Editions: Foil, Holographic, Polychrome, Negative.\n" +
        "Use 'antes' (array) to scope a clause, e.g. [1] for ante 1 only.\n\n" +
        "Randomly samples up to 1,000,000 seeds. Returns the top matches by score.",
      inputSchema: {
        jaml: z
          .string()
          .describe(
            'JAML filter JSON. Example: {"deck":"Red","stake":"White","must":[{"joker":"Blueprint","antes":[1]}]}'
          ),
        seed_count: z
          .number()
          .int()
          .min(1)
          .max(MAX_RANDOM_SEEDS)
          .default(DEFAULT_RANDOM_SEEDS)
          .describe(
            `Seeds to randomly sample (1–${MAX_RANDOM_SEEDS.toLocaleString()}). Default: ${DEFAULT_RANDOM_SEEDS.toLocaleString()}`
          ),
      },
      _meta: {
        ui: { resourceUri: SEARCH_UI_URI },
      },
    },
    async ({ jaml, seed_count }) => {
      try {
        const output = await searchSeeds(jaml, seed_count);
        const truncNote =
          output.resultsShown !== output.totalMatches
            ? ` Showing top ${output.resultsShown} of ${output.totalMatches}.`
            : "";
        return {
          structuredContent: output as unknown as Record<string, unknown>,
          content: [
            {
              type: "text" as const,
              text:
                `Search complete: ${output.matchesFound} matches from ` +
                `${Number(output.seedsSearched).toLocaleString()} seeds.${truncNote}`,
            },
          ],
        };
      } catch (err) {
        return {
          isError: true,
          content: [
            {
              type: "text" as const,
              text: `Search error: ${(err as Error).message}`,
            },
          ],
        };
      }
    }
  );

  // analyze_seed ────────────────────────────────────────────────────────────
  server.tool(
    "analyze_seed",
    "Inspect a specific Balatro seed: boss blinds, tags, vouchers, shop items, packs per ante. " +
      "Use after search_seeds to drill into a promising seed, or to check a known seed. " +
      "Deck/stake come from the supplied JAML (defaults: Red, White).",
    {
      seed: z.string().describe("Balatro seed string (e.g. 'ABCD1234')"),
      jaml: z
        .string()
        .describe('JAML filter JSON for deck/stake context. Minimal: {"deck":"Red","stake":"White"}'),
    },
    async ({ seed, jaml }) => {
      try {
        const ctx = await analyzeSeed(seed, jaml);
        return {
          content: [{ type: "text" as const, text: JSON.stringify(ctx, null, 2) }],
        };
      } catch (err) {
        return {
          isError: true,
          content: [
            { type: "text" as const, text: `Analysis error: ${(err as Error).message}` },
          ],
        };
      }
    }
  );

  // validate_jaml ──────────────────────────────────────────────────────────
  server.tool(
    "validate_jaml",
    "Validate a JAML filter. Returns 'valid' or the engine's descriptive error. Does NOT run a search.",
    {
      jaml: z.string().describe("JAML filter JSON string to validate"),
    },
    async ({ jaml }) => {
      try {
        await bootPromise;
        MotelyWasmHost.loadJaml(jaml);
        return {
          content: [{ type: "text" as const, text: "JAML is valid." }],
        };
      } catch (err) {
        return {
          content: [
            { type: "text" as const, text: `Invalid JAML: ${(err as Error).message}` },
          ],
        };
      }
    }
  );

  // get_version ────────────────────────────────────────────────────────────
  server.tool(
    "get_version",
    "Get the MotelyJAML engine version string.",
    {},
    async () => {
      await bootPromise;
      return {
        content: [
          {
            type: "text" as const,
            text: `MotelyJAML v${MotelyWasmHost.getVersion()}`,
          },
        ],
      };
    }
  );
}
