import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";

const { Motely } = harness;

// JS-side parity for Motely.Tests/JamlFilterTypeTests.cs.
//
// The C# tests prove every JAML filter clause shape "compiles and runs": parse
// the JAML, build a search, run a tiny batch, assert it searched seeds and
// completed. We mirror that through the WASM API — fromJaml (compiles) +
// runSeedListSearch over a fixed seed list (runs) — asserting the search
// completes and actually evaluated the filter against the listed seeds.
//
// We do NOT assert on matches: like the C# matrix, the contract under test is
// "this clause shape is accepted and executes", not "these seeds hit".

const SEEDS = ["AAAAAAAA", "BBBBBBBB", "CCCCCCCC", "12345678", "ALEEB"];

function compilesAndRuns(yaml) {
    const cfg = Motely.fromJaml(yaml);
    cfg.seeds = SEEDS;
    const r = Motely.runSeedListSearch(cfg);
    assert.equal(r.isCompleted, true, "search should complete");
    assert.equal(
        Number(r.totalSeedsSearched),
        SEEDS.length,
        "should have evaluated every listed seed",
    );
}

// Build a `must:` document from a single clause, mirroring the C# helper's
// `clause.Replace("\n", "\n    ")` indentation so multi-line clauses nest under
// the list item.
function mustClause(clause) {
    return `must:\n  - ${clause.replace(/\n/g, "\n    ")}`;
}

function runClauseCases(title, clauses, wrap = mustClause) {
    describe(title, () => {
        for (const clause of clauses) {
            it(`compiles and runs: ${clause.replace(/\n/g, " / ")}`, () => {
                compilesAndRuns(wrap(clause));
            });
        }
    });
}

runClauseCases("JamlFilterDesc — joker syntax variations", [
    "joker: Showman",
    "joker: Showman\nedition: Negative",
    "joker: Showman\nedition: Polychrome\nstickers: [Eternal]",
    "jokers: [Showman, Blueprint]",
]);

runClauseCases("JamlFilterDesc — joker rarity filters", [
    "commonJoker: HalfJoker",
    "uncommonJoker: Showman",
    "rareJoker: Blueprint",
    "legendaryJoker: Perkeo",
]);

runClauseCases("JamlFilterDesc — voucher filters", [
    "voucher: Telescope",
    "vouchers: [Telescope, Observatory]",
]);

runClauseCases("JamlFilterDesc — consumable filters", [
    "tarotCard: TheEmperor",
    "tarotCard: TheFool",
    "tarotCards: [TheFool, TheEmperor]",
    "spectralCard: Familiar",
    "spectralCard: Aura",
    "spectralCards: [Familiar, Aura]",
    "planetCard: Earth",
    "planetCard: Pluto",
]);

runClauseCases("JamlFilterDesc — boss filters", [
    "boss: TheArm",
    "boss: TheWall",
]);

runClauseCases("JamlFilterDesc — tag filters", [
    "tag: CouponTag",
    "smallBlindTag: CouponTag",
    "bigBlindTag: RareTag",
]);

runClauseCases("JamlFilterDesc — standard card filters", [
    "standardCard: HA",
    "standardCard: SA\nenhancement: Lucky",
    "standardCard: C2\nseal: Red\nedition: Foil",
]);

runClauseCases(
    "JamlFilterDesc — erratic deck filters",
    ["erraticRank: A", "erraticSuit: Spades", "erraticCard: SA"],
    (clause) => `deck: Erratic\n${mustClause(clause)}`,
);

runClauseCases("JamlFilterDesc — starting draw filters", [
    "startingDraw: HA",
]);

runClauseCases("JamlFilterDesc — event filters", [
    "event: LuckyMoney",
    "event: LuckyMult",
    "event: MisprintMult",
    "event: WheelOfFortune",
    "event: CavendishExtinct",
    "event: GrosMichelExtinct",
]);

describe("JamlFilterDesc — sources targeting", () => {
    it("compiles and runs a clause with explicit sources", () => {
        compilesAndRuns(`must:
  - joker: Showman
    sources:
      shopItems: [1, 2]
      boosterPacks: [1, 2, 3]
      judgement: [1]
      wraith: [1]
      riffRaff: [1]
      rareTag: [1]
      uncommonTag: [1]
  - tarotCard: TheEmperor
    sources:
      shopItems: [1]
      boosterPacks: [1]
      emperor: [1, 2]
      purpleSealOrEightBall: [1]
  - spectralCard: Aura
    sources:
      sixthSense: [1]
      seance: [1]
  - standardCard: HA
    sources:
      certificate: [1]
      incantation: [1]
      familiar: [1]
      grim: [1]
      deckDraw: [1, 2, 3]`);
    });
});

describe("JamlFilterDesc — logical combinators", () => {
    it("compiles and runs and/or/min across must/should/mustNot", () => {
        compilesAndRuns(`must:
  - or:
      - joker: Showman
      - joker: Blueprint
  - and:
      - tarotCard: TheFool
      - spectralCard: Aura
  - joker: HalfJoker
    min: 2
should:
  - boss: TheArm
mustNot:
  - joker: Vagabond`);
    });
});
