// Builds the JAML authoring context that primes an LLM to translate a
// natural-language seed-search request into a valid JAML filter document.
//
// The enum vocabularies are read live from jaml.schema.json so they never
// drift from the engine. The prose grammar and curated examples are hand
// maintained here because they teach intent, not just shape.

import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));

const SCHEMA_CANDIDATES = [
  process.env.JAML_SCHEMA_PATH,
  join(here, "..", "..", "..", "jaml.schema.json"), // repo root, when run in-tree
  join(here, "..", "jaml.schema.json"), // bundled copy, when installed from npm
].filter(Boolean);

export function loadSchema() {
  for (const path of SCHEMA_CANDIDATES) {
    try {
      return { schema: JSON.parse(readFileSync(path, "utf8")), path };
    } catch {
      // try next candidate
    }
  }
  throw new Error(
    `jaml.schema.json not found. Looked in:\n  ${SCHEMA_CANDIDATES.join("\n  ")}`,
  );
}

function enumValues(schema, defName) {
  return schema?.$defs?.[defName]?.enum ?? [];
}

function list(values) {
  return values.length ? values.join(", ") : "(none)";
}

// Real filters from JamlFilters/, plus one illustrative mustNot example.
// These are the few-shot anchors — keep them small, valid, and varied.
export const CURATED_EXAMPLES = [
  {
    name: "Minimal must + should",
    teaches: "smallest valid document; deck/stake; one hard clause, one scoring clause",
    jaml: `name: simple_test
deck: Red
stake: White
must:
  - joker: Blueprint
should:
  - voucher: Telescope`,
  },
  {
    name: "Antes, sources, or-branches, scored should",
    teaches: "restricting clauses to antes; sources.boosterPacks; an `or` group; editions in scoring",
    jaml: `name: Perkeo Observatory
description: Telescope ante 1, Observatory ante 2, Perkeo from a Soul card in an early booster pack.
stake: White
must:
  - voucher: Telescope
    antes: [1]
  - voucher: Observatory
    antes: [2]
  - or:
      - legendaryJoker: Perkeo
        antes: [1]
        sources:
          boosterPacks: [0]
      - legendaryJoker: Perkeo
        antes: [2]
        sources:
          boosterPacks: [0, 1, 2]
should:
  - legendaryJoker: Perkeo
    antes: [1]
    score: 10
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [1, 2]
    score: 20`,
  },
  {
    name: "Tarot/joker chain with source provenance",
    teaches: "tag/uncommonJoker/tarotCard discriminators; sources.judgement and sources.emperor",
    jaml: `name: Fool Emperor Showman Chain
deck: Magic
stake: White
must:
  - tag: CharmTag
    antes: [1]
  - uncommonJoker: Showman
    antes: [1]
    sources:
      judgement: [0]
  - tarotCard: TheEmperor
    antes: [1]
    sources:
      boosterPacks: [1]
should:
  - tarotCard: TheEmperor
    antes: [1]
    sources:
      emperor: [0, 1, 2]
    score: 8
  - rareJoker: Blueprint
    antes: [1]
    sources:
      emperor: [1, 2]
    score: 1`,
  },
  {
    name: "Editions + stickers + scoring",
    teaches: "edition and stickers as shared clause properties; scoring the same item multiple ways",
    jaml: `name: Idol
deck: Erratic
stake: Black
must:
  - legendaryJoker: Perkeo
should:
  - joker: TheIdol
    stickers:
      - Eternal
    score: 10
  - joker: Showman
    edition: Negative
    stickers:
      - Eternal
    score: 10
  - legendaryJoker: Perkeo
    score: 10`,
  },
  {
    name: "Wildcard + min + mustNot (illustrative)",
    teaches: "`Any` wildcard; `min` match count; a `mustNot` rejection clause",
    jaml: `name: clean-early-rares
description: At least two rare jokers by ante 1, but never a Perkeo in antes 1-3.
deck: Red
stake: White
must:
  - rareJoker: Any
    antes: [1]
    min: 2
mustNot:
  - legendaryJoker: Perkeo
    antes: [1, 2, 3]
should:
  - rareJoker: Blueprint
    antes: [1]
    score: 5`,
  },
];

export function buildGuide(schema) {
  const decks = enumValues(schema, "MotelyDeck");
  const stakes = enumValues(schema, "MotelyStake");
  const jokersAll = enumValues(schema, "MotelyJoker");
  const jokersCommon = enumValues(schema, "MotelyJokerCommon");
  const jokersUncommon = enumValues(schema, "MotelyJokerUncommon");
  const jokersRare = enumValues(schema, "MotelyJokerRare");
  const jokersLegendary = enumValues(schema, "MotelyJokerLegendary");
  const vouchers = enumValues(schema, "MotelyVoucher");
  const tarots = enumValues(schema, "MotelyTarotCard");
  const spectrals = enumValues(schema, "MotelySpectralCard");
  const planets = enumValues(schema, "MotelyPlanetCard");
  const bosses = enumValues(schema, "MotelyBossBlind");
  const tags = enumValues(schema, "MotelyTag");
  const editions = enumValues(schema, "MotelyItemEdition");
  const enhancements = enumValues(schema, "MotelyItemEnhancement");
  const seals = enumValues(schema, "MotelyItemSeal");
  const stickers = enumValues(schema, "MotelyJokerSticker");
  const events = enumValues(schema, "MotelyEventType");

  const examples = CURATED_EXAMPLES.map(
    (e) => `### ${e.name}\n_Teaches: ${e.teaches}_\n\n\`\`\`yaml\n${e.jaml}\n\`\`\``,
  ).join("\n\n");

  return `# JAML authoring guide

JAML — Jimbo's Ante Markup Language — is the YAML-based filter language for the
Motely Balatro seed-search engine. A JAML document describes what a seed must
contain, must not contain, and what makes one seed score higher than another.
**JAML is not generic YAML**: every identifier is a strict PascalCase enum value
and the parser rejects unknown keys and English-cased names.

## Document shape

A JAML document is a YAML mapping with these top-level keys:

| Key | Type | Notes |
|---|---|---|
| \`name\` | string | optional metadata |
| \`author\` | string | optional metadata |
| \`description\` | string | optional metadata |
| \`deck\` | enum | optional; one of MotelyDeck |
| \`stake\` | enum | optional; one of MotelyStake |
| \`defaults\` | mapping | optional; \`antes\`, \`boosterPacks\`, \`shopItems\`, \`score\` applied to every clause that omits them |
| \`must\` | clause[] | hard requirements — every clause must match (logical AND) |
| \`should\` | clause[] | scoring — each matching clause adds its \`score\` to the seed total |
| \`mustNot\` | clause[] | rejection — if any clause matches, the seed is discarded |
| \`seeds\` | string[] | optional; restrict the search to an explicit seed list |

**At least one of \`must\`, \`should\`, or \`mustNot\` is required.** Omit \`deck\`/\`stake\`
unless the request implies them.

## Clause shape

Each entry in \`must\`/\`should\`/\`mustNot\` is a mapping with **exactly one
discriminator key** plus optional shared properties.

Discriminator keys (pick one per clause):

- Jokers by rarity: \`joker\`, \`commonJoker\`, \`uncommonJoker\`, \`rareJoker\`, \`legendaryJoker\`
  - Plural array forms: \`jokers\`, \`commonJokers\`, \`uncommonJokers\`, \`rareJokers\`, \`legendaryJokers\` — match if any listed joker is found
  - Any singular joker key accepts the wildcard value \`Any\` (e.g. \`rareJoker: Any\`)
- Consumables / shop: \`voucher\` (or \`vouchers\`), \`tarotCard\` (or \`tarotCards\`), \`spectralCard\` (or \`spectralCards\`), \`planetCard\`
- Blinds & tags: \`boss\`, \`tag\`, \`smallBlindTag\`, \`bigBlindTag\`
- Standard cards: \`standardCard\` / \`standardCards\` (string like \`"AceOfSpades"\`, or a mapping with \`rank\`/\`suit\`/\`seal\`/\`enhancement\`/\`edition\`)
- Erratic deck: \`erraticRank\`, \`erraticSuit\`, \`erraticCard\`, \`startingDraw\`
- Events: \`event\` (one of MotelyEventType). Each event also has a shorthand discriminator that takes an array of qualifying rolls, e.g. \`luckyMoney: [0, 1, 2]\`, \`businessPayout: [...]\`, \`spaceLevelup: [...]\`.
- Logic groups: \`and\`, \`or\`, \`clauses\` — each takes an array of nested clauses

Shared properties (optional, combine with any discriminator):

- \`antes\` — int array, which antes to check (default \`[1..8]\`). The property is \`antes\` — never \`byAnte\`/\`atAnte\`.
- \`score\` — integer, only meaningful inside \`should\` (negatives allowed)
- \`min\` / \`max\` — required / allowed match count (default \`min: 1\`)
- \`label\` — free-text label for the clause, shows up in results
- \`edition\` — one of: ${list(editions)}
- \`stickers\` — array of: ${list(stickers)}
- \`seal\` — one of: ${list(seals)}
- \`enhancement\` — one of: ${list(enhancements)}
- \`sources\` — restrict where the item may come from. Common sub-keys:
  - \`shopItems\` / \`boosterPacks\` — int arrays of slot indices (0-based)
  - \`minShopItem\` / \`maxShopItem\` — int slot bounds
  - \`judgement\`, \`emperor\`, \`wraith\`, \`soulCard\`, \`sixthSense\`, \`seance\`, \`familiar\`, \`grim\`, \`incantation\`, \`certificate\`, \`riffRaff\` — int arrays; the item must be produced by that source at one of those indices
  - \`arcanaPacks\`, \`spectralPacks\` — int arrays of pack indices
  - \`tags\`, \`charmTag\`, \`etherealTag\`, \`requireMega\` — booleans

## Naming convention — strict PascalCase

Every enum value is a PascalCase identifier with no spaces and no punctuation:
\`LuckyCat\` (not "Lucky Cat"), \`MrBones\` (not "Mr. Bones"), \`OopsAll6s\`
(not "Oops! All 6s"), \`Cloud9\`, \`ToTheMoon\`, \`RideTheBus\`, \`HalfJoker\`,
\`ChaostheClown\`. Tarot cards take a \`The\` prefix where the card name has one:
\`TheFool\`, \`TheEmperor\`, \`TheHangedMan\`, but \`Justice\`, \`Strength\`,
\`Death\`, \`Temperance\`, \`Judgement\`. Never invent a value that is not in the
lists below — if the request names something with no enum, say so instead of
guessing.

## Enum vocabularies

**MotelyDeck**: ${list(decks)}

**MotelyStake**: ${list(stakes)}

**MotelyJoker** (use with \`joker\` / \`jokers\`): ${list(jokersAll)}

**MotelyJokerCommon** (use with \`commonJoker\`): ${list(jokersCommon)}

**MotelyJokerUncommon** (use with \`uncommonJoker\`): ${list(jokersUncommon)}

**MotelyJokerRare** (use with \`rareJoker\`): ${list(jokersRare)}

**MotelyJokerLegendary** (use with \`legendaryJoker\`): ${list(jokersLegendary)}

**MotelyVoucher**: ${list(vouchers)}

**MotelyTarotCard**: ${list(tarots)}

**MotelySpectralCard**: ${list(spectrals)}

**MotelyPlanetCard**: ${list(planets)}

**MotelyBossBlind**: ${list(bosses)}

**MotelyTag**: ${list(tags)}

**MotelyEventType**: ${list(events)}

## Worked examples

${examples}

## How to translate a request

1. Decide which named items map to which discriminator key. A joker's rarity
   determines the key — e.g. Blueprint is rare, so \`rareJoker: Blueprint\`; or
   use the rarity-agnostic \`joker:\` if unsure.
2. Anything the user calls a hard requirement goes in \`must\`. Anything they
   call "bonus", "prefer", "ideally", "worth more" goes in \`should\` with a
   \`score\`. Anything they want to avoid goes in \`mustNot\`.
3. Apply \`antes\`, \`edition\`, \`stickers\`, \`enhancement\`, \`seal\`, and
   \`sources\` only when the request states them.
4. Set \`deck\`/\`stake\` only if implied; otherwise omit them.
5. Map every name to an exact enum value above. If a requested item has no enum,
   do not invent one — flag it.
6. Output a single valid JAML (YAML) document in a \`\`\`yaml code block and
   nothing else. Keep it minimal: no clause properties the request did not ask
   for.
7. Confirm it: pass the document to the \`validate_jaml\` tool. If it returns an
   error, fix the document and re-validate until it passes.`;
}

// Splits the rendered guide into addressable sections so jaml_reference can
// return a focused slice instead of the whole guide.
export function getSections(guide) {
  const sections = [];
  const parts = guide.split(/\n(?=## )/);
  for (const part of parts) {
    const match = part.match(/^#{1,2} (.+)$/m);
    const title = match ? match[1].trim() : "overview";
    const slug = title
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/(^-|-$)/g, "");
    sections.push({ title, slug, body: part.trim() });
  }
  return sections;
}

export function buildPrimer(guide, request, opts = {}) {
  const { deck, stake } = opts;
  const hints = [];
  if (deck) hints.push(`The user indicated the deck should be: ${deck}`);
  if (stake) hints.push(`The user indicated the stake should be: ${stake}`);
  const hintBlock = hints.length ? `\n\nExtra constraints:\n- ${hints.join("\n- ")}` : "";

  return `${guide}

---

# Your task

Translate the following natural-language seed-search request into one valid
JAML document. Follow every rule above — strict PascalCase enums, exactly one
discriminator key per clause, only the properties the request asks for. Output
the JAML in a single \`\`\`yaml code block. If any requested item has no matching
enum value, still produce the closest valid document and note the unmatched
item in one line beneath the code block.

Then call the \`validate_jaml\` tool with the document. If it reports an error,
fix the document and validate again until it passes.

## Request

${request}${hintBlock}`;
}
