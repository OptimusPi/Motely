/**
 * MCP tool registration for the Balatro Seed MCP.
 *
 * The C# engine (motely-wasm) owns JAML parsing, validation, and search.
 * This file boots Bootsharp, registers tools, and forwards inputs
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

const MAX_RANDOM_SEEDS = 1_000_000;
const DEFAULT_RANDOM_SEEDS = 1_000_000;
const MAX_RESULTS = 200;

export const bootPromise = dotnet.boot();

// ── MCP Apps UI ──────────────────────────────────────────────────────────────

const SEARCH_UI_URI = "ui://balatro-seed-finder/jaml-search-app.html";

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
  configId: string,
  seedCount: number = DEFAULT_RANDOM_SEEDS
): Promise<SearchResponse> {
  await bootPromise;
  const results: SearchResult[] = [];

  return new Promise<SearchResponse>((resolve, reject) => {
    function onResult(seed: string, score: number, tally: Int32Array): void {
      results.push({ seed, score, tally: Array.from(tally) });
    }

    function onComplete(
      status: string,
      seedsSearched: bigint,
      matchingSeeds: bigint
    ): void {
      SearchEvents.onResult.unsubscribe(onResult);
      SearchEvents.onComplete.unsubscribe(onComplete);
      const sorted = results.sort(function (a, b) { return b.score - a.score; });
      const shown = sorted.slice(0, MAX_RESULTS);
      resolve({
        status,
        seedsSearched: seedsSearched.toString(),
        matchesFound: matchingSeeds.toString(),
        totalMatches: String(sorted.length),
        resultsShown: String(shown.length),
        results: shown,
      });
    }

    SearchEvents.onResult.subscribe(onResult);
    SearchEvents.onComplete.subscribe(onComplete);

    try {
      MotelyWasmHost.startRandomSearch(configId, seedCount);
    } catch (err) {
      SearchEvents.onResult.unsubscribe(onResult);
      SearchEvents.onComplete.unsubscribe(onComplete);
      reject(err);
    }
  });
}

// ── Tool descriptions ────────────────────────────────────────────────────────

const SEARCH_DESCRIPTION =
  `Search for Balatro seeds using a JAML filter.\n` +
  `\n` +
  `## JAML (Jimbo's Ante Markup Language)\n` +
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

// ── Static catalog ──────────────────────────────────────────────────────────

const CATALOG = {
  decks: ["Red", "Blue", "Yellow", "Green", "Black", "Magic", "Nebula", "Ghost", "Abandoned", "Checkered", "Zodiac", "Painted", "Anaglyph", "Plasma", "Erratic"],
  stakes: ["White", "Red", "Green", "Black", "Blue", "Purple", "Orange", "Gold"],
  jokers: {
    common: ["Joker", "Greedy Joker", "Lusty Joker", "Wrathful Joker", "Gluttonous Joker", "Jolly Joker", "Zany Joker", "Mad Joker", "Crazy Joker", "Droll Joker", "Sly Joker", "Wily Joker", "Clever Joker", "Devious Joker", "Crafty Joker", "Half Joker", "Credit Card", "Banner", "Mystic Summit", "8 Ball", "Misprint", "Raised Fist", "Chaos the Clown", "Scary Face", "Abstract Joker", "Delayed Gratification", "Gros Michel", "Even Steven", "Odd Todd", "Scholar", "Business Card", "Supernova", "Ride the Bus", "Egg", "Runner", "Ice Cream", "Splash", "Blue Joker", "Faceless Joker", "Green Joker", "Superposition", "To Do List", "Cavendish", "Red Card", "Square Joker", "Riff-Raff", "Photograph", "Reserved Parking", "Mail-In Rebate", "Hallucination", "Fortune Teller", "Juggler", "Drunkard", "Golden Joker", "Popcorn", "Walkie Talkie", "Smiley Face", "Golden Ticket", "Swashbuckler", "Hanging Chad", "Shoot the Moon"],
    uncommon: ["Joker Stencil", "Four Fingers", "Mime", "Ceremonial Dagger", "Marble Joker", "Loyalty Card", "Dusk", "Fibonacci", "Steel Joker", "Hack", "Pareidolia", "Space Joker", "Burglar", "Blackboard", "Sixth Sense", "Constellation", "Hiker", "Card Sharp", "Madness", "Seance", "Vampire", "Shortcut", "Hologram", "Cloud 9", "Rocket", "Midas Mask", "Luchador", "Gift Card", "Turtle Bean", "Erosion", "To the Moon", "Stone Joker", "Lucky Cat", "Bull", "Diet Cola", "Trading Card", "Flash Card", "Spare Trousers", "Ramen", "Seltzer", "Castle", "Mr. Bones", "Acrobat", "Sock and Buskin", "Troubadour", "Certificate", "Smeared Joker", "Throwback", "Rough Gem", "Bloodstone", "Arrowhead", "Onyx Agate", "Glass Joker", "Showman", "Flower Pot", "Merry Andy", "Oops! All 6s", "The Idol", "Seeing Double", "Matador", "Satellite", "Cartomancer", "Astronomer", "Bootstraps"],
    rare: ["DNA", "Vagabond", "Baron", "Obelisk", "Baseball Card", "Ancient Joker", "Campfire", "Blueprint", "Wee Joker", "Hit the Road", "The Duo", "The Trio", "The Family", "The Order", "The Tribe", "Stuntman", "Invisible Joker", "Brainstorm", "Driver's License", "Burnt Joker"],
    legendary: ["Canio", "Triboulet", "Yorick", "Chicot", "Perkeo"],
  },
  vouchers: ["Overstock", "Overstock Plus", "Clearance Sale", "Liquidation", "Hone", "Glow Up", "Reroll Surplus", "Reroll Glut", "Crystal Ball", "Omen Globe", "Telescope", "Observatory", "Grabber", "Nacho Tong", "Wasteful", "Recyclomancy", "Tarot Merchant", "Tarot Tycoon", "Planet Merchant", "Planet Tycoon", "Seed Money", "Money Tree", "Blank", "Antimatter", "Magic Trick", "Illusion", "Hieroglyph", "Petroglyph", "Director's Cut", "Retcon", "Paint Brush", "Palette"],
  bosses: {
    normal: ["The Arm", "The Club", "The Eye", "The Fish", "The Flint", "The Goad", "The Head", "The Hook", "The House", "The Manacle", "The Mark", "The Mouth", "The Needle", "The Ox", "The Pillar", "The Plant", "The Psychic", "The Serpent", "The Tooth", "The Wall", "The Water", "The Wheel", "The Window"],
    finisher: ["Amber Acorn", "Cerulean Bell", "Crimson Heart", "Verdant Leaf", "Violet Vessel"],
  },
  tags: ["Uncommon Tag", "Rare Tag", "Negative Tag", "Foil Tag", "Holographic Tag", "Polychrome Tag", "Investment Tag", "Voucher Tag", "Boss Tag", "Standard Tag", "Charm Tag", "Meteor Tag", "Buffoon Tag", "Handy Tag", "Garbage Tag", "Ethereal Tag", "Coupon Tag", "Double Tag", "Juggle Tag", "D6 Tag", "Top-up Tag", "Speed Tag", "Orbital Tag", "Economy Tag"],
  editions: ["Foil", "Holographic", "Polychrome", "Negative"],
  stickers: ["Eternal", "Perishable", "Rental"],
  seals: ["Gold", "Red", "Blue", "Purple"],
  enhancements: ["Bonus", "Mult", "Wild", "Glass", "Steel", "Stone", "Gold", "Lucky"],
  clauseTypes: ["joker", "commonJoker", "uncommonJoker", "rareJoker", "legendaryJoker", "soulJoker", "voucher", "boss", "tag", "smallBlindTag", "bigBlindTag", "tarotCard", "spectralCard", "planetCard", "standardCard", "erraticRank", "erraticSuit", "erraticCard", "event"],
  sections: ["must", "should", "mustNot"],
};

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

  // get_version ────────────────────────────────────────────────────────────
  server.tool(
    "get_version",
    "Get the MotelyJAML engine version string.",
    {},
    async () => {
      await bootPromise;
      const ver = MotelyWasmHost.getVersion();
      return {
        content: [{ type: "text" as const, text: ver }],
      };
    }
  );

  // validate_jaml ──────────────────────────────────────────────────────────
  server.tool(
    "validate_jaml",
    "Validate a JAML filter string. Returns 'valid' if well-formed, or a descriptive error. Does NOT run a search.",
    {
      jaml: z
        .string()
        .describe("JAML filter JSON string to validate"),
    },
    async ({ jaml }) => {
      await bootPromise;
      try {
        const result = MotelyWasmHost.validateJaml(jaml);
        return {
          content: [{ type: "text" as const, text: result }],
        };
      } catch (err) {
        return {
          isError: true,
          content: [
            { type: "text" as const, text: `Validation error: ${(err as Error).message}` },
          ],
        };
      }
    }
  );

  // get_catalog ─────────────────────────────────────────────────────────────
  server.tool(
    "get_catalog",
    "Get the full catalog of valid Balatro item names for JAML filters (jokers, vouchers, bosses, tags, editions, etc.).",
    {
      category: z
        .enum(["all", "jokers", "vouchers", "bosses", "tags", "decks", "stakes", "editions", "stickers", "seals", "enhancements", "clauseTypes", "sections"])
        .default("all")
        .describe("Which category to return. Default: all."),
    },
    async ({ category }) => {
      const data = category === "all" ? CATALOG : { [category]: (CATALOG as Record<string, unknown>)[category] };
      return {
        content: [{ type: "text" as const, text: JSON.stringify(data, null, 2) }],
      };
    }
  );

  // get_jaml_schema ────────────────────────────────────────────────────────
  server.tool(
    "get_jaml_schema",
    "Get the JAML filter JSON schema describing valid filter structure, clause types, and options.",
    {},
    async () => {
      const schemaPath = fileURLToPath(new URL("../jaml.schema.json", import.meta.url));
      const text = await readFile(schemaPath, "utf-8");
      return {
        content: [{ type: "text" as const, text }],
      };
    }
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
        jaml: z
          .string()
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
    async ({ jaml, seed_count }) => {
      let configId: string;
      try {
        await bootPromise;
        configId = MotelyWasmHost.loadJaml(jaml);
      } catch (err) {
        return {
          isError: true,
          content: [{ type: "text" as const, text: `Input error: ${(err as Error).message}` }],
        };
      }

      try {
        const output = await searchSeeds(configId, seed_count);
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
    ANALYZE_DESCRIPTION,
    {
      seed: z.string().describe("Balatro seed string (e.g. 'ABCD1234')"),
      jaml: z
        .string()
        .describe('JAML filter JSON for deck/stake. Minimal: {"deck":"Red","stake":"White"}'),
    },
    async ({ seed, jaml }) => {
      try {
        await bootPromise;
        // Parse the JAML to get deck/stake, then analyze
        const configId = MotelyWasmHost.loadJaml(jaml);
        const deck = MotelyWasmHost.getConfigDeck(configId);
        const stake = MotelyWasmHost.getConfigStake(configId);
        const jsonStr = MotelyWasmHost.analyzeSeed(seed, deck, stake);
        const parsed = JSON.parse(jsonStr);
        return {
          content: [{ type: "text" as const, text: JSON.stringify(parsed, null, 2) }],
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
