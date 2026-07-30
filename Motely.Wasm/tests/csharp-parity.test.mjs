import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";

const { MotelyJaml, MotelySearch, MotelyDeck, MotelyStake } = harness;
const parse = (text) => MotelyJaml.fromJaml(text);

async function searchList(yaml, seeds) {
    const config = parse(yaml);
    config.seeds = seeds;

    const bareMatches = [];
    let progress = null;
    const onM = (seed) => bareMatches.push(seed);
    const onP = (p) => { progress = p; };

    MotelySearch.onSeedMatch.subscribe(onM);
    MotelySearch.onProgress.subscribe(onP);
    let scoredMatches;
    try { scoredMatches = await MotelySearch.searchList(config); }
    finally {
        MotelySearch.onSeedMatch.unsubscribe(onM);
        MotelySearch.onProgress.unsubscribe(onP);
    }

    // The call resolves with the typed results; rows is the same data, plain-object shaped.
    const rows = scoredMatches.map((r) => ({
        seed: r.seed,
        score: Number(scoreOf(r)),
        tallies: Array.from(r.tallies ?? [], Number), // tallies cross as Int32Array; plain-array it

    }));
    return { bareMatches, rows, scoredMatches, progress };
}

function scoreOf(result) {
    return result?.score ?? result?.Score ?? 0;
}

describe("C# parity — JAML loader (engine only)", () => {
    it("fromJaml parses deck, stake, and top-level clause groups", () => {
        const config = MotelyJaml.fromJaml(`name: jaml happy
deck: Erratic
stake: Gold
must:
  - joker: Blueprint
should:
  - voucher: Telescope
    score: 5
mustNot:
  - joker: Vagabond
`);

        assert.equal(config.deck, MotelyDeck.Erratic);
        assert.equal(config.stake, MotelyStake.Gold);
        assert.equal(config.must.length, 1);
        assert.equal(config.should.length, 1);
        assert.equal(config.mustNot.length, 1);
    });

    it("validate rejects unknown root and clause keys loudly", () => {
        const root = MotelyJaml.validate(`name: t\nboses:\n  - joker: Blueprint\n`);
        const clause = MotelyJaml.validate(
            `name: t\nmust:\n  - joker: Blueprint\n    boosterPakcz: [0]\n`
        );
        assert.match(root, /boses/);
        assert.match(clause, /boosterPakcz/);
    });

    it("fromJaml throws on malformed input", () => {
        assert.throws(() => MotelyJaml.fromJaml("must: ["));
    });
});

describe("C# parity — scoring and default source behavior", () => {
    it("AND clauses tally complete conjunctions, not summed children", async () => {
        const both = await searchList(`name: and-min
deck: Red
stake: White
should:
  - and:
      - smallBlindTag: PolychromeTag
        antes: [1]
      - voucher: TarotMerchant
        antes: [1]
    score: 7
`, ["MOTELY77"]);
        assert.equal(both.scoredMatches.length, 1);
        assert.equal(scoreOf(both.scoredMatches[0]), 7);
        assert.deepEqual(both.rows[0].tallies, [1]);

        const missing = await searchList(`name: and-gate
deck: Red
stake: White
should:
  - and:
      - smallBlindTag: PolychromeTag
        antes: [1]
      - voucher: Telescope
        antes: [1]
    score: 7
`, ["MOTELY77"]);
        assert.equal(scoreOf(missing.scoredMatches[0]), 0);
        assert.deepEqual(missing.rows[0].tallies, [0]);
    });

    it("sourceless wildcard joker defaults match explicit shop-only sources", async () => {
        // Filter default (null sources:) is shop slots only — packs need an explicit block.
        const implicit = await searchList(`name: default-fallback
deck: Red
stake: White
should:
  - joker: Any
    score: 1
`, ["MOTELY77"]);
        const explicit = await searchList(`name: explicit-fallback
deck: Red
stake: White
should:
  - joker: Any
    score: 1
    antes: [1, 2, 3, 4, 5, 6, 7, 8]
    sources:
      shopItems: [0, 1, 2, 3, 4, 5, 6, 7]
`, ["MOTELY77"]);

        assert.equal(implicit.scoredMatches.length, 1);
        assert.ok(scoreOf(implicit.scoredMatches[0]) > 0);
        assert.equal(scoreOf(implicit.scoredMatches[0]), scoreOf(explicit.scoredMatches[0]));
    });

    it("explicit sources are not overwritten by wildcard defaults", async () => {
        const narrow = await searchList(`name: narrow-source
deck: Red
stake: White
should:
  - joker: Any
    score: 1
    antes: [1]
    sources:
      shopItems: [0]
`, ["MOTELY77"]);
        const wide = await searchList(`name: wide-source
deck: Red
stake: White
should:
  - joker: Any
    score: 1
`, ["MOTELY77"]);

        assert.ok(scoreOf(wide.scoredMatches[0]) >= scoreOf(narrow.scoredMatches[0]));
    });
});

describe("C# parity — Hieroglyph pack-slot reachability", () => {
    const seed = "KHTW99TC";

    it("matches Negative Perkeo in ante 1 slot 5 when the run state rewinds ante", async () => {
        const result = await searchList(`name: HieroglyphPerkeo
deck: Red
stake: White
must:
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [1]
    sources:
      boosterPacks: [5]
`, [seed]);
        assert.equal(Number(result.progress.seedsSearched), 1);
        assert.deepEqual(result.rows.map((row) => row.seed), [seed]);
    });

    it("does not match when restricted to normal ante-1 pack slots", async () => {
        const result = await searchList(`name: HieroglyphPerkeoRestricted
deck: Red
stake: White
must:
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [1]
    sources:
      boosterPacks: [0, 1, 2, 3]
`, [seed]);
        assert.equal(Number(result.progress.seedsSearched), 1);
        assert.deepEqual(result.rows.map((row) => row.seed), []);
    });

    it("rejects removed earlyAntesMaxPack syntax", () => {
        const error = MotelyJaml.validate(`name: RemovedEarlyAntesMaxPack
deck: Red
stake: White
must:
  - legendaryJoker: Perkeo
    edition: Negative
    antes: [1]
    sources:
      boosterPacks: [5]
      earlyAntesMaxPack: 5
`);
        assert.match(error, /earlyAntesMaxPack/);
    });

    it("bare legendary joker clause parses and runs", async () => {
        const result = await searchList(`name: HieroglyphPerkeoBare
deck: Red
stake: White
must:
  - legendaryJoker: Perkeo
`, [seed]);
        assert.equal(Number(result.progress.seedsSearched), 1);
    });
});

describe("C# parity — luck source multipliers", () => {
    it("luckyMoney roll 0 uses the sources.luck multiplier for filtering and scoring", async () => {
        const defaultLuck = await searchList(`name: LuckyMoneyDefaultLuck
deck: Red
stake: White
must:
  - luckyMoney: [0]
should:
  - luckyMoney: [0]
    label: lucky_money_r0_luck1
    score: 100
`, ["41111111"]);
        assert.equal(Number(defaultLuck.progress.seedsSearched), 1);
        assert.deepEqual(defaultLuck.scoredMatches, []);

        const luck5 = await searchList(`name: LuckyMoneyLuck5
deck: Red
stake: White
must:
  - luckyMoney: [0]
    with:
      luck: 5
should:
  - luckyMoney: [0]
    label: lucky_money_r0_luck5
    score: 100
    with:
      luck: 5
`, ["41111111"]);
        assert.equal(Number(luck5.progress.seedsSearched), 1);
        assert.equal(luck5.scoredMatches.length, 1);
        assert.equal(scoreOf(luck5.scoredMatches[0]), 100);
        assert.deepEqual(luck5.rows[0].tallies, [1]);
    });

    it("higher luckyMult luck matches at least the default-luck seeds", async () => {
        const seeds = ["41111111", "12345678", "UNITTEST", "ALEEBOOO", "ALEEB"];
        const defaultLuck = await searchList(`name: LuckyMultDefaultLuck
deck: Red
stake: White
must:
  - luckyMult: [0]
`, seeds);
        const luck5 = await searchList(`name: LuckyMultLuck5
deck: Red
stake: White
must:
  - luckyMult: [0]
    with:
      luck: 5
`, seeds);

        const defaultSet = new Set(defaultLuck.rows.map((row) => row.seed));
        const luck5Set = new Set(luck5.rows.map((row) => row.seed));
        assert.ok(luck5Set.size >= defaultSet.size);
        for (const seed of defaultSet)
            assert.ok(luck5Set.has(seed), `${seed} matched default luck but not luck 5`);
    });
});
