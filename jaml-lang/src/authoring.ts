// The JAML *authoring* contract: the shape you TYPE in a .jaml file.
//
// This is the POCO that never existed. `JamlConfig` (from motely-wasm) describes
// the PARSED output (packed Int32Array). This describes the INPUT — `joker:
// WeeJoker`, `antes: [1]`, `sources: { emperor: [0] }` — with real enum unions
// pulled from the C# source of truth via codegen.
//
// One schema, four payoffs:
//   • `z.infer` static types         -> jaml-ui binds forms/editor to these
//   • `.parse()` runtime validation  -> cheap client-side "is this well-formed"
//   • the enum unions                -> LSP / CodeMirror completion vocab
//   • `z.toJSONSchema()` (on demand) -> the dead artifact, only if ever forced
//
// Mirrors Motely/Filters/Jaml/JamlConfigLoader.Models.cs (JamlClauseUnion).
// The engine (parseJaml) is still the FINAL authority on semantics; this is the
// fast structural gate in front of it.

import { z } from "zod";
import {
  MotelyJoker,
  MotelyJokerCommon,
  MotelyJokerUncommon,
  MotelyJokerRare,
  MotelyJokerLegendary,
  MotelyVoucher,
  MotelyTarotCard,
  MotelySpectralCard,
  MotelyPlanetCard,
  MotelyBossBlind,
  MotelyTag,
  MotelyEventType,
  MotelyItemEdition,
  MotelyItemSeal,
  MotelyItemEnhancement,
  MotelyJokerSticker,
  MotelyStandardcardRank,
  MotelyStandardcardSuit,
  MotelyDeck,
  MotelyStake,
} from "./vocab.generated.js";

// z.enum wants a non-empty tuple; the generated arrays are `readonly`.
const e = <T extends readonly [string, ...string[]] | readonly string[]>(arr: T) =>
  z.enum(arr as unknown as [string, ...string[]]);

// `joker: Any` is legal anywhere a single enum is (EnumOrAny<T> in C#).
const orAny = (arr: readonly string[]) => z.union([e(arr), z.literal("Any")]);

const ante = z.number().int().min(1).max(8);
const antes = z.array(ante);
const slots = z.array(z.number().int().min(0)); // pack/shop slot indices

// JamlSources — the full "where did it come from" surface (Models.cs:317).
export const SourcesSchema = z
  .object({
    shopItems: slots,
    boosterPacks: slots,
    minShopItem: z.number().int(),
    maxShopItem: z.number().int(),
    earlyAntesMaxPack: z.number().int(),
    tags: z.boolean(),
    requireMega: z.boolean(),
    charmTag: z.boolean(),
    etherealTag: z.boolean(),
    // Tarot / spectral provenance streams
    judgement: slots,
    wraith: slots,
    rareTag: slots,
    uncommonTag: slots,
    soulCard: slots,
    arcanaPacks: slots,
    spectralPacks: slots,
    riffRaff: slots,
    purpleSealOrEightBall: slots,
    emperor: slots,
    sixthSense: slots,
    seance: slots,
    certificate: slots,
    incantation: slots,
    familiar: slots,
    grim: slots,
    deckDraw: slots,
    uncommonShopJokers: slots,
    rareShopJokers: slots,
    commonShopJokers: slots,
    allShopJokers: slots,
  })
  .partial();

// StandardCard can be a bare string ("Ace_of_Spades"-ish) or a structured object.
const StandardCardConfig = z
  .object({
    rank: e(MotelyStandardcardRank),
    suit: e(MotelyStandardcardSuit),
    seal: e(MotelyItemSeal),
    enhancement: e(MotelyItemEnhancement),
    edition: e(MotelyItemEdition),
    sources: SourcesSchema,
  })
  .partial();
const StandardCardValue = z.union([z.string(), StandardCardConfig]);

// Properties common to every clause (Models.cs:196+).
const commonProps = {
  antes: antes.optional(),
  score: z.number().int().optional(),
  min: z.number().int().optional(),
  max: z.number().int().optional(),
  label: z.string().optional(),
  edition: e(MotelyItemEdition).optional(),
  stickers: z.array(e(MotelyJokerSticker)).optional(),
  seal: e(MotelyItemSeal).optional(),
  enhancement: e(MotelyItemEnhancement).optional(),
  rank: e(MotelyStandardcardRank).optional(),
  suit: e(MotelyStandardcardSuit).optional(),
  rolls: slots.optional(),
  soulEditionRolls: z.number().int().optional(),
  soulCardOnly: z.boolean().optional(),
  // flat source shortcuts allowed top-level on a clause
  shopItems: slots.optional(),
  boosterPacks: slots.optional(),
  minShopItem: z.number().int().optional(),
  maxShopItem: z.number().int().optional(),
  judgement: slots.optional(),
  wraith: slots.optional(),
  rareTag: slots.optional(),
  uncommonTag: slots.optional(),
  sources: SourcesSchema.optional(),
};

// The item "selector" keys — exactly the ones that pick WHAT to match.
const selectorProps = {
  joker: orAny(MotelyJoker).optional(),
  jokers: z.array(e(MotelyJoker)).optional(),
  commonJoker: orAny(MotelyJokerCommon).optional(),
  commonJokers: z.array(e(MotelyJokerCommon)).optional(),
  uncommonJoker: orAny(MotelyJokerUncommon).optional(),
  uncommonJokers: z.array(e(MotelyJokerUncommon)).optional(),
  rareJoker: orAny(MotelyJokerRare).optional(),
  rareJokers: z.array(e(MotelyJokerRare)).optional(),
  legendaryJoker: orAny(MotelyJokerLegendary).optional(),
  legendaryJokers: z.array(e(MotelyJokerLegendary)).optional(),
  voucher: e(MotelyVoucher).optional(),
  vouchers: z.array(e(MotelyVoucher)).optional(),
  tarotCard: e(MotelyTarotCard).optional(),
  tarotCards: z.array(e(MotelyTarotCard)).optional(),
  spectralCard: e(MotelySpectralCard).optional(),
  spectralCards: z.array(e(MotelySpectralCard)).optional(),
  planetCard: e(MotelyPlanetCard).optional(),
  boss: e(MotelyBossBlind).optional(),
  tag: e(MotelyTag).optional(),
  tags: z.array(e(MotelyTag)).optional(),
  smallBlindTag: e(MotelyTag).optional(),
  smallBlindTags: z.array(e(MotelyTag)).optional(),
  bigBlindTag: e(MotelyTag).optional(),
  bigBlindTags: z.array(e(MotelyTag)).optional(),
  standardCard: StandardCardValue.optional(),
  standardCards: z.array(StandardCardValue).optional(),
  erraticRank: e(MotelyStandardcardRank).optional(),
  erraticSuit: e(MotelyStandardcardSuit).optional(),
  erraticCard: z.string().optional(),
  startingDraw: z.string().optional(),
  event: e(MotelyEventType).optional(),
  // numeric event clauses (the int[] "roll budget" family)
  luckyMoney: slots.optional(),
  luckyMult: slots.optional(),
  misprintMult: slots.optional(),
  wheelOfFortune: slots.optional(),
  cavendishExtinct: slots.optional(),
  grosMichelExtinct: slots.optional(),
  spaceLevelup: slots.optional(),
  businessPayout: slots.optional(),
  bloodstoneTrigger: slots.optional(),
  parkingPayout: slots.optional(),
  glassDestroy: slots.optional(),
  wheelStaysFlipped: slots.optional(),
};

const SELECTOR_KEYS = Object.keys(selectorProps) as (keyof typeof selectorProps)[];

// A clause is recursive (and/or/clauses nest other clauses), so the type is
// declared up front and the schema is built with z.lazy.
export type JamlClause = z.infer<typeof ClauseSchema>;

export const ClauseSchema: z.ZodType<Record<string, unknown>> = z.lazy(() =>
  z
    .object({
      ...commonProps,
      ...selectorProps,
      // compound logic
      and: z.array(ClauseSchema).optional(),
      or: z.array(ClauseSchema).optional(),
      clauses: z.array(ClauseSchema).optional(),
      mode: z.string().optional(),
    })
    .superRefine((c, ctx) => {
      const hasSelector = SELECTOR_KEYS.some((k) => c[k] !== undefined);
      const hasCompound =
        (c.and?.length ?? 0) > 0 ||
        (c.or?.length ?? 0) > 0 ||
        (c.clauses?.length ?? 0) > 0;
      if (!hasSelector && !hasCompound) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message:
            "clause must select something (e.g. `joker:`, `voucher:`) or compose with `and`/`or`/`clauses`",
        });
      }
    })
);

export const DefaultsSchema = z
  .object({
    antes,
    boosterPacks: slots,
    shopItems: slots,
    score: z.number().int(),
  })
  .partial();

// The whole filter. `id` is the only structurally-required field.
export const JamlConfigSchema = z
  .object({
    id: z.string(),
    name: z.string().optional(),
    author: z.string().optional(),
    dateCreated: z.string().optional(),
    description: z.string().optional(),
    deck: e(MotelyDeck).optional(),
    stake: e(MotelyStake).optional(),
    defaults: DefaultsSchema.optional(),
    must: z.array(ClauseSchema).optional(),
    should: z.array(ClauseSchema).optional(),
    mustNot: z.array(ClauseSchema).optional(),
    seeds: z.array(z.string()).optional(),
    hashtags: z.array(z.string()).optional(),
  })
  .strict(); // unknown root keys are an error — mirrors the C# strict loader

export type JamlConfigInput = z.infer<typeof JamlConfigSchema>;
export type JamlSources = z.infer<typeof SourcesSchema>;
