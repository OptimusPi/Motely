/**
 * MCP tool registration for the Balatro Seed MCP.
 *
 * The C# engine (motely-wasm) owns JAML parsing, validation, and search.
 * This file boots Bootsharp, registers two tools, and forwards inputs
 * straight to the engine. Engine errors propagate as isError MCP responses.
 */
import { readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import {
  registerAppResource,
  registerAppTool,
  RESOURCE_MIME_TYPE,
} from "@modelcontextprotocol/ext-apps/server";
import type { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import dotnet, { MotelyWasmHost, SearchEvents } from "motely-wasm";
import { z } from "zod";

type SingleSearchContext = ReturnType<typeof MotelyWasmHost.motelySingleSearchContext>;

const MAX_RANDOM_SEEDS = 1_000_000;
const DEFAULT_RANDOM_SEEDS = 1_000_000;
const MAX_RESULTS = 200;

export const bootPromise = dotnet.boot();

// ── MCP Apps UI ──────────────────────────────────────────────────────────────

const SEARCH_UI_URI = "ui://balatro-seed-mcp/jaml-search-app.html";

async function loadSearchAppHtml(): Promise<string> {
  const html = fileURLToPath(new URL("../mcp-ui/dist/jaml-search-app.html", import.meta.url));
  return readFile(html, "utf-8");
}

// ── Search ───────────────────────────────────────────────────────────────────

interface SearchResult {
  seed: string;
  score: number;
  tally: number[];
}

interface SearchResponse {
  status: string;
  seedsSearched: string;
  matchesFound: string;
  totalMatches: string;
  resultsShown: string;
  results: SearchResult[];
}

export async function searchSeeds(
  jamlJson: string,
  seedCount: number = DEFAULT_RANDOM_SEEDS
): Promise<SearchResponse> {
  await bootPromise;
  const results: SearchResult[] = [];

  return new Promise<SearchResponse>((resolve, reject) => {
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
      const config = MotelyWasmHost.loadJaml(jamlJson);
      MotelyWasmHost.startRandomSearch(config, seedCount);
    } catch (err) {
      SearchEvents.onResult.unsubscribe(onResult);
      SearchEvents.onComplete.unsubscribe(onComplete);
      reject(err);
    }
  });
}

// ── Single-seed analysis ─────────────────────────────────────────────────────

export async function analyzeSeed(
  seed: string,
  jamlJson: string
): Promise<SingleSearchContext> {
  await bootPromise;
  const config = MotelyWasmHost.loadJaml(jamlJson);
  return MotelyWasmHost.motelySingleSearchContext(seed, config.deck, config.stake);
}

// ── Tool descriptions ────────────────────────────────────────────────────────

const SEARCH_DESCRIPTION =
  `Search for Balatro seeds. Describe what you want in natural language (Jummy), or write a JAML filter.\n` +
  `\n` +
  `## Jummy (natural language — use this for simple searches)\n` +
  `Just say what you're looking for:\n` +
  `  "what: Blueprint in ante 1"\n` +
  `  "what: Negative Perkeo in ante 1"\n` +
  `  "what: eternal Brainstorm in ante 2"\n` +
  `  "what: Blueprint\\nwhere: deck Red, stake Gold"\n` +
  `\n` +
  `## JAML (Jimbo's Ante Markup Language — for complex filters)\n` +
  `JAML is a YAML-based language for describing seed criteria.\n` +
  `Example JAML filter:\n` +
  `  deck: Red\n` +
  `  stake: White\n` +
  `  must:\n` +
  `    - joker: Blueprint\n` +
  `      antes: [1]\n` +
  `      edition: Negative\n` +
  `    - voucher: Telescope\n` +
  `      antes: [1, 2]\n` +
  `  should:\n` +
  `    - joker: Brainstorm\n` +
  `      antes: [1, 2, 3]\n` +
  `      score: 10\n` +
  `\n` +
  `Pass JAML as a JSON object (the engine parses both YAML and JSON forms).\n` +
  `\n` +
  `## Sections\n` +
  `- must: all clauses must match (required)\n` +
  `- should: scored clauses (higher score = better seed, not required)\n` +
  `- mustNot: reject seed if any clause matches\n` +
  `\n` +
  `## Clause types\n` +
  `joker, commonJoker, uncommonJoker, rareJoker, legendaryJoker, soulJoker, ` +
  `voucher, boss, tag, smallBlindTag, bigBlindTag, ` +
  `tarotCard, spectralCard, planetCard, standardCard, ` +
  `erraticRank, erraticSuit, erraticCard, event\n` +
  `\n` +
  `## Clause options\n` +
  `- antes: [1] or [1,2,3] — which antes to check (0 = pre-game)\n` +
  `- edition: Foil, Holographic, Polychrome, Negative\n` +
  `- stickers: [Eternal], [Perishable], [Rental]\n` +
  `- seal: Gold, Red, Blue, Purple\n` +
  `- enhancement: Bonus, Mult, Wild, Glass, Steel, Stone, Gold, Lucky\n` +
  `- score: number (for should clauses)\n` +
  `- label: display name for the clause\n` +
  `\n` +
  `## Decks\n` +
  `Red, Blue, Yellow, Green, Black, Magic, Nebula, Ghost, Abandoned, Checkered, Zodiac, Painted, Anaglyph, Plasma, Erratic\n` +
  `\n` +
  `## Stakes\n` +
  `White, Red, Green, Black, Blue, Purple, Orange, Gold\n` +
  `\n` +
  `Randomly samples up to 1,000,000 seeds (~200-1200ms). Returns top matches by score.`;

const ANALYZE_DESCRIPTION =
  `Inspect a specific Balatro seed. Returns the full run breakdown for each ante (1-8):\n` +
  `- Boss blind for each ante\n` +
  `- Tags (small blind, big blind)\n` +
  `- Vouchers available\n` +
  `- Shop items and jokers\n` +
  `- Booster packs and their contents\n` +
  `\n` +
  `Use after search_seeds to drill into a promising seed, or to check a known seed.\n` +
  `Deck and stake come from the JAML filter (defaults: Red deck, White stake).`;

// ── MCP tool registration ────────────────────────────────────────────────────

export function registerTools(server: McpServer) {
  registerAppResource(
    server,
    "JAML Search UI",
    SEARCH_UI_URI,
    {
      description:
        "Balatro seed search viewer for MCP Apps hosts.",
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
      description: SEARCH_DESCRIPTION,
      annotations: { readOnlyHint: true },
      inputSchema: {
        jummy: z
          .string()
          .optional()
          .describe(
            'Natural language seed search. Examples: "what: Negative Perkeo in ante 1", ' +
            '"what: Blueprint and Brainstorm in ante 2\\nwhere: deck Red, stake Gold"'
          ),
        jaml: z
          .string()
          .optional()
          .describe(
            'JAML filter as JSON. Example: {"deck":"Red","stake":"White","must":[{"joker":"Blueprint","antes":[1],"edition":"Negative"}]}'
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
    async ({ jummy, jaml, seed_count }) => {
      let jamlInput: string;
      try {
        if (jummy && jaml) {
          return {
            isError: true,
            content: [{ type: "text" as const, text: "Provide jummy OR jaml, not both." }],
          };
        }
        if (jummy) {
          await bootPromise;
          const config = MotelyWasmHost.compileJummy(jummy);
          jamlInput = JSON.stringify(config);
        } else if (jaml) {
          jamlInput = jaml;
        } else {
          return {
            isError: true,
            content: [{ type: "text" as const, text: "Provide jummy or jaml parameter." }],
          };
        }
      } catch (err) {
        return {
          isError: true,
          content: [{ type: "text" as const, text: `Input error: ${(err as Error).message}` }],
        };
      }

      try {
        const output = await searchSeeds(jamlInput, seed_count);
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
          content: [{ type: "text" as const, text: `Search error: ${(err as Error).message}` }],
        };
      }
    }
  );

  // analyze_seed ────────────────────────────────────────────────────────────
  server.tool(
    "analyze_seed",
    {
      description: ANALYZE_DESCRIPTION,
      annotations: { readOnlyHint: true },
    },
    {
      seed: z.string().describe("Balatro seed string (e.g. 'ABCD1234')"),
      jaml: z
        .string()
        .describe('JAML filter JSON for deck/stake. Minimal: {"deck":"Red","stake":"White"}'),
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
}
