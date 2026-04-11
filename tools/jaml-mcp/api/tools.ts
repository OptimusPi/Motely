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
import type { Analysis } from "motely-wasm-compat";
import dotnet, { Motely, MotelyWasmHost, SearchEvents } from "motely-wasm-compat";
import YAML from "yaml";
import { z } from "zod";

const MAX_RANDOM_SEEDS = 1_000_000;
const DEFAULT_RANDOM_SEEDS = 1_000_000;
export const bootPromise = dotnet.boot();
type SearchSession = { cancel(): void };
type MotelyWasmHostExtended = typeof MotelyWasmHost & {
  startRandomSearchFromJaml(jaml: string, seedCount: number): SearchSession;
};
const Host = MotelyWasmHost as MotelyWasmHostExtended;

const SPECIAL_DISPLAY: Record<string, string> = {
  EightBall: "8 Ball",
  Cloud9: "Cloud 9",
  OopsAll6s: "Oops! All 6s",
  ToTheMoon: "To the Moon",
  ToDoList: "To Do List",
  RiffRaff: "Riff-raff",
  MailInRebate: "Mail In Rebate",
  SockAndBuskin: "Sock and Buskin",
  DriversLicense: "Driver's License",
  DirectorsCut: "Director's Cut",
  PlanetX: "Planet X",
  MrBones: "Mr. Bones",
  ChaostheClown: "Chaos the Clown",
  ShootTheMoon: "Shoot the Moon",
  RideTheBus: "Ride the Bus",
  HitTheRoad: "Hit the Road",
  VerdantLeaf: "Verdant Leaf",
  VioletVessel: "Violet Vessel",
  CrimsonHeart: "Crimson Heart",
  AmberAcorn: "Amber Acorn",
  CeruleanBell: "Cerulean Bell",
  TheWheelOfFortune: "The Wheel of Fortune",
  TheHighPriestess: "The High Priestess",
  TheHangedMan: "The Hanged Man",
  TheMagician: "The Magician",
  TheHierophant: "The Hierophant",
  TheLovers: "The Lovers",
  TheHermit: "The Hermit",
  TheDevil: "The Devil",
  TheTower: "The Tower",
  TheWorld: "The World",
  TheFool: "The Fool",
  TheStar: "The Star",
  TheMoon: "The Moon",
  TheSun: "The Sun",
};

function normalizeToken(value: string): string {
  return value.replace(/[^a-z0-9]/gi, "").toLowerCase();
}

function displayName(name: string): string {
  if (SPECIAL_DISPLAY[name]) return SPECIAL_DISPLAY[name];
  return name.replace(/([A-Z])/g, (m, c, i) => (i > 0 ? " " : "") + c).trim();
}

function createEnumLookup(enumObj?: Record<string, unknown>): Map<string, string> {
  const lookup = new Map<string, string>();
  if (!enumObj) return lookup;
  for (const key of Object.keys(enumObj)) {
    if (/^\d+$/.test(key)) continue;
    const canonical = key;
    lookup.set(normalizeToken(canonical), canonical);
    lookup.set(normalizeToken(displayName(canonical)), canonical);
  }
  return lookup;
}

function createLookupWithExtras(
  enumObj: Record<string, unknown> | undefined,
  extras: string[] = []
): Map<string, string> {
  const lookup = createEnumLookup(enumObj);
  for (const value of extras) lookup.set(normalizeToken(value), value);
  return lookup;
}

const ROOT_KEY_LOOKUP = new Map<string, string>(
  [
    "id",
    "name",
    "description",
    "author",
    "dateCreated",
    "deck",
    "stake",
    "seeds",
    "hashtags",
    "defaults",
    "must",
    "should",
    "mustNot",
    "aesthetics",
  ].map((k) => [k.toLowerCase(), k])
);

const CLAUSE_KEY_LOOKUP = new Map<string, string>(
  [
    "type",
    "value",
    "eventType",
    "joker",
    "jokers",
    "commonJoker",
    "commonJokers",
    "uncommonJoker",
    "uncommonJokers",
    "rareJoker",
    "rareJokers",
    "mixedJoker",
    "mixedJokers",
    "soulJoker",
    "legendaryJoker",
    "voucher",
    "vouchers",
    "tarot",
    "tarotCard",
    "spectral",
    "spectralCard",
    "planet",
    "planetCard",
    "boss",
    "tag",
    "smallBlindTag",
    "bigBlindTag",
    "standardCard",
    "erraticRank",
    "erraticSuit",
    "erraticCard",
    "startingDraw",
    "event",
    "antes",
    "score",
    "min",
    "max",
    "label",
    "edition",
    "stickers",
    "seal",
    "enhancement",
    "rank",
    "suit",
    "rolls",
    "luckyMoney",
    "luckyMult",
    "misprintMult",
    "wheelOfFortune",
    "cavendishExtinct",
    "grosMichelExtinct",
    "soulEditionRolls",
    "soulCardOnly",
    "multValue",
    "shopItems",
    "boosterPacks",
    "minShopSlot",
    "maxShopSlot",
    "minPackSlot",
    "maxPackSlot",
    "sources",
    "and",
    "or",
  ].map((k) => [k.toLowerCase(), k])
);

const VALUE_LOOKUPS = {
  deck: createEnumLookup(Motely.MotelyDeck as unknown as Record<string, unknown>),
  stake: createEnumLookup(Motely.MotelyStake as unknown as Record<string, unknown>),
  joker: createLookupWithExtras(Motely.MotelyJoker as unknown as Record<string, unknown>, [
    "Any",
    "AnyCommon",
    "AnyUncommon",
    "AnyRare",
    "AnyLegendary",
  ]),
  jokers: createLookupWithExtras(Motely.MotelyJoker as unknown as Record<string, unknown>, [
    "Any",
    "AnyCommon",
    "AnyUncommon",
    "AnyRare",
    "AnyLegendary",
  ]),
  commonJoker: createLookupWithExtras(
    Motely.MotelyJokerCommon as unknown as Record<string, unknown>,
    ["Any", "AnyCommon"]
  ),
  uncommonJoker: createLookupWithExtras(
    Motely.MotelyJokerUncommon as unknown as Record<string, unknown>,
    ["Any", "AnyUncommon"]
  ),
  rareJoker: createLookupWithExtras(
    Motely.MotelyJokerRare as unknown as Record<string, unknown>,
    ["Any", "AnyRare"]
  ),
  legendaryJoker: createLookupWithExtras(undefined, ["Any", "AnyLegendary"]),
  voucher: createEnumLookup(Motely.MotelyVoucher as unknown as Record<string, unknown>),
  vouchers: createEnumLookup(Motely.MotelyVoucher as unknown as Record<string, unknown>),
  boss: createEnumLookup(Motely.MotelyBossBlind as unknown as Record<string, unknown>),
  tag: createEnumLookup(Motely.MotelyTag as unknown as Record<string, unknown>),
  tarot: createEnumLookup(Motely.MotelyTarotCard as unknown as Record<string, unknown>),
  tarotCard: createEnumLookup(Motely.MotelyTarotCard as unknown as Record<string, unknown>),
  spectral: createEnumLookup(Motely.MotelySpectralCard as unknown as Record<string, unknown>),
  spectralCard: createEnumLookup(
    Motely.MotelySpectralCard as unknown as Record<string, unknown>
  ),
  planet: createEnumLookup(Motely.MotelyPlanetCard as unknown as Record<string, unknown>),
  planetCard: createEnumLookup(Motely.MotelyPlanetCard as unknown as Record<string, unknown>),
  edition: createEnumLookup(Motely.MotelyItemEdition as unknown as Record<string, unknown>),
  stickers: createEnumLookup(Motely.MotelyJokerSticker as unknown as Record<string, unknown>),
  seal: createEnumLookup(Motely.MotelyItemSeal as unknown as Record<string, unknown>),
  enhancement: createEnumLookup(
    Motely.MotelyItemEnhancement as unknown as Record<string, unknown>
  ),
  rank: createEnumLookup(Motely.MotelyPlayingCardRank as unknown as Record<string, unknown>),
  suit: createEnumLookup(Motely.MotelyPlayingCardSuit as unknown as Record<string, unknown>),
  erraticRank: createEnumLookup(
    Motely.MotelyPlayingCardRank as unknown as Record<string, unknown>
  ),
  erraticSuit: createEnumLookup(
    Motely.MotelyPlayingCardSuit as unknown as Record<string, unknown>
  ),
} as const;

function canonicalizeScalar(
  key: string,
  value: unknown,
  changed: { value: boolean }
): unknown {
  if (typeof value !== "string") return value;
  const lookup = (VALUE_LOOKUPS as Record<string, Map<string, string> | undefined>)[key];
  if (!lookup) return value;
  const mapped = lookup.get(normalizeToken(value));
  if (mapped && mapped !== value) {
    changed.value = true;
    return mapped;
  }
  return value;
}

function canonicalizeByKey(
  key: string,
  value: unknown,
  changed: { value: boolean }
): unknown {
  if (Array.isArray(value)) {
    return value.map((item) => canonicalizeScalar(key, item, changed));
  }
  return canonicalizeScalar(key, value, changed);
}

function canonicalizeObject(
  input: Record<string, unknown>,
  changed: { value: boolean }
): Record<string, unknown> {
  const out: Record<string, unknown> = {};
  for (const [rawKey, rawValue] of Object.entries(input)) {
    const rootCanonical = ROOT_KEY_LOOKUP.get(rawKey.toLowerCase());
    const clauseCanonical = CLAUSE_KEY_LOOKUP.get(rawKey.toLowerCase());
    const canonicalKey = rootCanonical ?? clauseCanonical ?? rawKey;
    if (canonicalKey !== rawKey) changed.value = true;

    let value: unknown;
    if (Array.isArray(rawValue)) {
      value = rawValue.map((item) =>
        item && typeof item === "object" && !Array.isArray(item)
          ? canonicalizeObject(item as Record<string, unknown>, changed)
          : canonicalizeByKey(canonicalKey, item, changed)
      );
    } else if (rawValue && typeof rawValue === "object") {
      value = canonicalizeObject(rawValue as Record<string, unknown>, changed);
    } else {
      value = canonicalizeByKey(canonicalKey, rawValue, changed);
    }

    out[canonicalKey] = value;
  }
  return out;
}

function normalizeJamlCaseInsensitive(
  jamlJson: string
): { jamlJson: string; changed: boolean } {
  let parsed: unknown;
  try {
    parsed = JSON.parse(jamlJson);
  } catch {
    // If it's not JSON text, leave untouched and let Motely return the real parse error.
    return { jamlJson, changed: false };
  }
  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
    return { jamlJson, changed: false };
  }

  const changed = { value: false };
  const normalized = canonicalizeObject(parsed as Record<string, unknown>, changed);
  if (!changed.value) return { jamlJson, changed: false };
  return { jamlJson: JSON.stringify(normalized), changed: true };
}

/** MCP Apps: bundled React + json-render UI for `search_seeds` (Claude / Copilot / other hosts). */
const SEARCH_UI_URI = "ui://jaml-mcp/jaml-search-app.html";

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

// ── JAML parsing ─────────────────────────────────────────────────────────────

function callCompileJummy(jummy: string): unknown {
  return MotelyWasmHost.compileJummy(jummy);
}

function parseIntSafe(value: unknown): number | undefined {
  const n = Number(value);
  return Number.isInteger(n) ? n : undefined;
}

function maybeCanonicalizeValueByKey(key: string, value: string): string {
  const lookup = (VALUE_LOOKUPS as Record<string, Map<string, string> | undefined>)[key];
  return lookup?.get(normalizeToken(value)) ?? value;
}

function parseSimpleMumbleLine(line: string): Record<string, unknown> | null {
  const m = line
    .trim()
    .match(
      /^(?:(?<sticker>eternal|perishable|rental)\s+)?(?<joker>.+?)(?:\s+(?:in|by)\s+ante\s+(?<ante>\d+))?$/i
    );
  if (!m?.groups?.joker) return null;
  const joker = maybeCanonicalizeValueByKey("joker", m.groups.joker.trim());
  const clause: Record<string, unknown> = { joker };
  if (m.groups.sticker) {
    clause.stickers = [maybeCanonicalizeValueByKey("stickers", m.groups.sticker)];
  }
  if (m.groups.ante) {
    const ante = parseIntSafe(m.groups.ante);
    if (ante) clause.antes = [ante];
  }
  return clause;
}

function parseWhereDeckStake(source: unknown): {
  deck?: string;
  stake?: string;
  antes?: number[];
} {
  const out: { deck?: string; stake?: string; antes?: number[] } = {};
  if (!source) return out;
  if (typeof source === "string") {
    const deckMatch = source.match(/deck\s+([A-Za-z]+)/i);
    const stakeMatch = source.match(/stake\s+([A-Za-z]+)/i);
    const anteMatch = source.match(/ante(?:s)?\s+(\d+)/i);
    if (deckMatch?.[1]) out.deck = maybeCanonicalizeValueByKey("deck", deckMatch[1]);
    if (stakeMatch?.[1]) out.stake = maybeCanonicalizeValueByKey("stake", stakeMatch[1]);
    if (anteMatch?.[1]) {
      const ante = parseIntSafe(anteMatch[1]);
      if (ante) out.antes = [ante];
    }
    return out;
  }
  if (typeof source === "object" && !Array.isArray(source)) {
    const obj = source as Record<string, unknown>;
    for (const [rawKey, rawVal] of Object.entries(obj)) {
      const key = rawKey.toLowerCase().replace(/\s+/g, "");
      if (key === "deck" && typeof rawVal === "string") {
        out.deck = maybeCanonicalizeValueByKey("deck", rawVal);
      } else if (key === "stake" && typeof rawVal === "string") {
        out.stake = maybeCanonicalizeValueByKey("stake", rawVal);
      } else if ((key === "ante" || key === "antes") && rawVal != null) {
        if (Array.isArray(rawVal)) {
          out.antes = rawVal
            .map((x) => parseIntSafe(x))
            .filter((x): x is number => typeof x === "number");
        } else {
          const ante = parseIntSafe(rawVal);
          if (ante) out.antes = [ante];
        }
      }
    }
  }
  return out;
}

function normalizeWhatClause(source: unknown): Record<string, unknown> {
  if (typeof source === "string") {
    return parseSimpleMumbleLine(source) ?? { joker: source };
  }
  if (source && typeof source === "object" && !Array.isArray(source)) {
    const changed = { value: false };
    return canonicalizeObject(source as Record<string, unknown>, changed);
  }
  throw new Error("Unsupported Jummy 'what' clause.");
}

function compileJummyFallback(jummy: string): Record<string, unknown> {
  const parsed = YAML.parse(jummy);
  if (!parsed || typeof parsed !== "object" || Array.isArray(parsed)) {
    throw new Error("Jummy fallback expects a YAML object.");
  }
  const root = parsed as Record<string, unknown>;
  const where = parseWhereDeckStake(root.where);
  const out: Record<string, unknown> = {};
  if (where.deck) out.deck = where.deck;
  if (where.stake) out.stake = where.stake;

  const must: Array<Record<string, unknown>> = [];

  if (root.what != null) {
    const clause = normalizeWhatClause(root.what);
    if (where.antes?.length && clause.antes == null) clause.antes = where.antes;
    must.push(clause);
  }

  if (Array.isArray(root.must)) {
    for (const item of root.must) {
      if (typeof item === "string") {
        const clause = parseSimpleMumbleLine(item);
        if (clause) must.push(clause);
        continue;
      }
      if (item && typeof item === "object" && !Array.isArray(item)) {
        const raw = item as Record<string, unknown>;
        const clause = normalizeWhatClause(raw.what ?? raw);
        const subWhere = parseWhereDeckStake(raw.where);
        if (subWhere.antes?.length && clause.antes == null) clause.antes = subWhere.antes;
        if (!out.deck && subWhere.deck) out.deck = subWhere.deck;
        if (!out.stake && subWhere.stake) out.stake = subWhere.stake;
        must.push(clause);
      }
    }
  }

  if (must.length > 0) out.must = must;

  const changed = { value: false };
  return canonicalizeObject(out, changed);
}

export type CompileJummyOk = { ok: true; jamlYaml: string; jamlJson: string };
export type CompileJummyErr = { ok: false; error: string };

/** Compile + validate Jummy via WASM; returns JSON string for search or errors. */
export async function compileJummyPayload(
  jummy: string
): Promise<CompileJummyOk | CompileJummyErr> {
  await bootPromise;
  let jamlConfig: any;
  try {
    jamlConfig = callCompileJummy(jummy);
  } catch (e) {
    try {
      jamlConfig = compileJummyFallback(jummy);
    } catch (fallbackErr) {
      return {
        ok: false,
        error:
          `Jummy compile failed (${(e as Error).message}). ` +
          `Fallback parser also failed (${(fallbackErr as Error).message}).`,
      };
    }
  }
  try {
    return {
      ok: true,
      jamlYaml: YAML.stringify(jamlConfig),
      jamlJson: JSON.stringify(jamlConfig),
    };
  } catch (e) {
    return { ok: false, error: (e as Error).message };
  }
}

export async function resolveJamlOrJummy(
  jaml?: string | undefined,
  jummy?: string | undefined
): Promise<string> {
  const j = jaml?.trim() ?? "";
  const y = jummy?.trim() ?? "";
  if (!j && !y) throw new Error("Provide jaml or jummy");
  if (j && y) throw new Error("Provide only one of jaml or jummy");
  if (y) {
    const c = await compileJummyPayload(y);
    if (!c.ok) throw new Error(c.error);
    return c.jamlJson;
  }
  return j;
}

/** Parse a JAML JSON string into a config object. Throws on invalid JSON. */
export function parseJaml(jamlJson: string): any {
  let config: any;
  try {
    config = JSON.parse(jamlJson);
  } catch (e) {
    throw new Error(`Invalid JSON: ${(e as Error).message}`);
  }
  if (typeof config !== "object" || config === null || Array.isArray(config)) {
    throw new Error("JAML must be a JSON object");
  }
  return config;
}

// ── Seed search ──────────────────────────────────────────────────────────────

export interface SearchResult {
  seed: string;
  score: number;
  tally: Int32Array | number[];
}

export interface SearchResponse {
  status: string;
  seedsSearched: string;
  matchesFound: string;
  totalMatches?: string;
  resultsShown?: string;
  results: SearchResult[];
}

const MAX_RESULTS = 200;

function normalizeSearchResponse(output: SearchResponse): SearchResponse {
  const sorted = output.results
    .map((row) => ({
      ...row,
      tally: Array.isArray(row.tally) ? row.tally : Array.from(row.tally),
    }))
    .sort((a, b) => b.score - a.score);
  return {
    ...output,
    totalMatches: String(sorted.length),
    resultsShown: String(Math.min(sorted.length, MAX_RESULTS)),
    results: sorted.slice(0, MAX_RESULTS),
  };
}

/**
 * Run a random seed search. Returns matching seeds, scores, and tally columns.
 * `jamlJson` must be a valid JAML JSON string (see jaml.schema.json).
 */
export async function searchSeeds(
  jamlJson: string,
  seedCount = DEFAULT_RANDOM_SEEDS
): Promise<SearchResponse> {
  await bootPromise;

  return new Promise<SearchResponse>((resolve, reject) => {
    const results: SearchResult[] = [];

    const onResult = (seed: string, score: number, tally: Int32Array) => {
      results.push({ seed, score, tally });
    };

    const onComplete = (
      status: string,
      seedsSearched: bigint,
      matchingSeeds: bigint
    ) => {
      SearchEvents.onResult.unsubscribe(onResult);
      SearchEvents.onComplete.unsubscribe(onComplete);
      resolve({
        status,
        seedsSearched: seedsSearched.toString(),
        matchesFound: matchingSeeds.toString(),
        results,
      });
    };

    SearchEvents.onResult.subscribe(onResult);
    SearchEvents.onComplete.subscribe(onComplete);

    try {
      Host.startRandomSearchFromJaml(jamlJson, seedCount);
    } catch (err) {
      SearchEvents.onResult.unsubscribe(onResult);
      SearchEvents.onComplete.unsubscribe(onComplete);
      reject(err);
    }
  });
}

// ── Seed analysis ─────────────────────────────────────────────────────────────

export async function analyzeSeed(
  seed: string,
  jamlJson: string
): Promise<Analysis.IMotelySingleSearchContextImpl> {
  await bootPromise;
  const jamlConfig = MotelyWasmHost.loadJaml(jamlJson);
  return MotelyWasmHost.motelySingleSearchContext(seed, jamlConfig.deck, jamlConfig.stake);
}

// ── MCP tool registration ─────────────────────────────────────────────────────

export function registerTools(server: McpServer) {
  registerAppResource(
    server,
    "JAML Search UI",
    SEARCH_UI_URI,
    {
      description:
        "Balatro seed search viewer (React + Vercel json-render) for MCP Apps hosts.",
    },
    async () => {
      const text = await loadSearchAppHtml();
      return {
        contents: [
          {
            uri: SEARCH_UI_URI,
            mimeType: RESOURCE_MIME_TYPE,
            text,
          },
        ],
      };
    }
  );

  // ── search_seeds (+ MCP App UI in compliant clients) ───────────────────────
  registerAppTool(
    server,
    "search_seeds",
    {
      title: "Search Balatro seeds",
      description:
        "Search for Balatro seeds matching a JAML filter. " +
        "Randomly samples up to 1,000,000 seeds (~200-1200 ms). " +
        "Returns top matching seeds with scores and per-clause tally breakdown.\n\n" +
        "Accepts JAML (JSON) or Jummy (YAML helper syntax). Case-insensitive.\n\n" +
        "JAML examples:\n" +
        '  {"must":[{"joker":"Blueprint"}]} — find Blueprint joker\n' +
        '  {"deck":"Red","stake":"Gold","must":[{"joker":"Blueprint"},{"voucher":"Telescope"}]} — multi-clause\n' +
        '  {"must":[{"joker":"Blueprint","antes":[1],"edition":"Negative"}]} — specific ante + edition\n' +
        '  {"must":[{"boss":"TheEye"}],"mustNot":[{"boss":"TheNeedle"}]} — require/exclude bosses\n\n' +
        "Jummy examples (simpler syntax):\n" +
        "  what: Blueprint in ante 1\n" +
        "  where: deck red, stake gold\n\n" +
        "Clause types: joker, voucher, boss, tag, tarotCard, spectralCard, planetCard, standardCard, edition, stickers, seal, enhancement",
      inputSchema: {
        jaml: z
          .string()
          .optional()
          .describe(
            'JAML filter JSON. Example: {"deck":"Red","stake":"White","must":[{"joker":"Blueprint"}]}'
          ),
        jummy: z
          .string()
          .optional()
          .describe(
            "Optional Jummy input (case-insensitive helper syntax). Example: 'what: blueprint in ante 1\\nwhere: deck red, stake white'"
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
    async ({ jaml, jummy, seed_count }) => {
      let jamlInput: string;
      let casingNormalized = false;
      try {
        jamlInput = await resolveJamlOrJummy(jaml, jummy);
      } catch (err) {
        return {
          isError: true,
          content: [
            {
              type: "text" as const,
              text: `Input error: ${(err as Error).message}`,
            },
          ],
        };
      }

      if (jaml && !jummy) {
        const normalized = normalizeJamlCaseInsensitive(jamlInput);
        jamlInput = normalized.jamlJson;
        casingNormalized = normalized.changed;
      }

      let output: SearchResponse;
      try {
        output = await searchSeeds(jamlInput, seed_count);
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

      const normalized = normalizeSearchResponse(output);
      const truncNote =
        normalized.resultsShown !== normalized.totalMatches
          ? ` Showing top ${normalized.resultsShown} of ${normalized.totalMatches}.`
          : "";
      return {
        structuredContent: normalized as unknown as Record<string, unknown>,
        content: [
          {
            type: "text" as const,
            text:
              `Search complete: ${normalized.matchesFound} matches from ` +
              `${Number(normalized.seedsSearched).toLocaleString()} seeds.${truncNote}` +
              (casingNormalized ? " (Case-insensitive normalization applied.)" : ""),
          },
        ],
      };
    }
  );

  // ── analyze_seed ────────────────────────────────────────────────────────────
  server.tool(
    "analyze_seed",
    "Inspect a specific Balatro seed. Returns full run details: boss blinds, tags, vouchers, shop items, and booster packs for each ante. " +
    "Use after search_seeds to drill into a promising seed, or to check a known seed. " +
    "Deck and stake are read from the JAML filter (defaults: Red deck, White stake).",
    {
      seed: z.string().describe("Balatro seed string (e.g. 'ABCD1234')"),
      jaml: z.string().describe('JAML filter JSON for deck/stake context. Minimal: {"deck":"Red","stake":"White"}'),
    },
    async ({ seed, jaml }) => {
      try {
        const result = await analyzeSeed(seed, jaml);
        return {
          content: [{ type: "text" as const, text: JSON.stringify(result, null, 2) }],
        };
      } catch (err) {
        return {
          content: [{ type: "text" as const, text: `Analysis error: ${(err as Error).message}` }],
        };
      }
    }
  );

  // ── validate_jaml ───────────────────────────────────────────────────────────
  server.tool(
    "validate_jaml",
    "Validate a JAML filter string. Returns 'valid' if the JSON is well-formed JAML, " +
    "or a descriptive error. Does NOT run a search.",
    {
      jaml: z.string().describe("JAML filter JSON string to validate"),
    },
    async ({ jaml }) => {
      await bootPromise;

      try {
        MotelyWasmHost.loadJaml(jaml);
        return {
          content: [{ type: "text" as const, text: "JAML is valid." }],
        };
      } catch (err) {
        return {
          content: [
            {
              type: "text" as const,
              text: `Invalid JAML: ${(err as Error).message}`,
            },
          ],
        };
      }
    }
  );

  // ── get_version ─────────────────────────────────────────────────────────────
  server.tool(
    "get_version",
    "Get the MotelyJAML engine version string.",
    {},
    async () => {
      await bootPromise;
      const version = MotelyWasmHost.getVersion();
      return {
        content: [{ type: "text" as const, text: `MotelyJAML v${version}` }],
      };
    }
  );
}
