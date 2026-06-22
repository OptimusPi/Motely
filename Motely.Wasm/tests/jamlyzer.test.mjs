import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { jaml } from "./fixtures.mjs";

const { MotelyJamlyzer, MotelyVoucher } = harness;

// Every per-ante composite-stream member the state bag must reconstruct on resume. Mirrors the C#
// AnalyzerUnitTests sweep — if offset-replay were wrong for any of these, its concat would diverge.
const PULLS_MEMBERS = [
    "judgementJokers", "wraithJokers", "emperorTarots", "purpleSealTarots",
    "sixthSenseSpectrals", "seanceSpectrals", "riffRaffJokers", "rareTagJokers",
    "uncommonTagJokers", "legendaryJokers", "voucherSequence",
];
const SHOP_MEMBERS = [
    "shopJokers", "commonShopJokers", "uncommonShopJokers", "rareShopJokers",
    "shopTarots", "shopPlanets", "shopSpectrals",
];
const EVENT_MEMBERS = [
    "luckyMoney", "luckyMult", "wheelOfFortune", "cavendish", "grosMichel", "space",
    "business", "bloodstone", "parking", "eightBall", "glass", "omenGlobe", "theWheel", "misprint",
];

describe("MotelyJamlyzer", () => {
    it("analyzeSeeds returns one result per seed, each with 8 antes", () => {
        const results = MotelyJamlyzer.analyzeSeeds(jaml.seeds);
        assert.equal(results.length, 2);
        assert.equal(results[0].seed, "UNITTEST");
        assert.equal(results[1].seed, "ALEEB");
        assert.equal(results[0].antes.length, 8);
    });

    it("ante structure: ante 1 has 4 packs, antes 2-8 have 6, numbers run 1..8", () => {
        const [r] = MotelyJamlyzer.analyzeSeeds(jaml.oneSeed);
        assert.equal(r.antes[0].packs.length, 4, "ante 1 -> 4 packs");
        for (let i = 1; i < 8; i++)
            assert.equal(r.antes[i].packs.length, 6, `ante ${i + 1} -> 6 packs`);
        for (let i = 0; i < 8; i++)
            assert.equal(r.antes[i].ante, i + 1, "ante numbers are sequential");
    });

    it("paged window sizes every stream to the roll count (Emperor x2)", () => {
        const rolls = 7;
        const [r] = MotelyJamlyzer.analyzeSeedsPaged(jaml.oneSeed, rolls);
        for (const m of EVENT_MEMBERS)
            assert.equal(r.events[m].length, rolls, `events.${m} length`);
        const a1 = r.antes[0];
        for (const m of PULLS_MEMBERS) {
            const want = m === "emperorTarots" ? rolls * 2 : rolls;
            assert.equal(a1.pulls[m].length, want, `pulls.${m} length`);
        }
        for (const m of SHOP_MEMBERS)
            assert.equal(a1.shopStreams[m].length, rolls, `shopStreams.${m} length`);
    });

    it("each result carries the resumable stream-state bag", () => {
        const [first] = MotelyJamlyzer.analyzeSeeds(jaml.seeds);
        assert.ok(first.streamStates, "streamStates present");
        assert.equal(typeof first.streamStates.rollOffset, "number");
    });

    it("resume: page1(10) ++ page2(10) reconstructs the full 20-window across EVERY stream", () => {
        const full = MotelyJamlyzer.analyzeSeedsPaged(jaml.oneSeed, 20)[0];
        const p1 = MotelyJamlyzer.analyzeSeedsPaged(jaml.oneSeed, 10)[0];
        const p2 = MotelyJamlyzer.resumeSeeds(jaml.oneSeed, p1.streamStates, 10)[0];

        assert.equal(p1.streamStates.rollOffset, 10);
        assert.equal(p2.streamStates.rollOffset, 20);

        // Event streams (resume by injected State double).
        for (const m of EVENT_MEMBERS)
            assert.deepEqual([...p1.events[m], ...p2.events[m]], [...full.events[m]], `events.${m}`);

        // Composite streams (resume by offset-replay) — every pulls + shop member, every ante.
        for (let a = 0; a < full.antes.length; a++) {
            for (const m of PULLS_MEMBERS)
                assert.deepEqual(
                    [...p1.antes[a].pulls[m], ...p2.antes[a].pulls[m]],
                    [...full.antes[a].pulls[m]], `ante${a + 1} pulls.${m}`);
            for (const m of SHOP_MEMBERS)
                assert.deepEqual(
                    [...p1.antes[a].shopStreams[m], ...p2.antes[a].shopStreams[m]],
                    [...full.antes[a].shopStreams[m]], `ante${a + 1} shopStreams.${m}`);
        }

        // Stitched end-state == full window's end-state (the decisive no-drift check).
        assert.deepEqual(p2.streamStates, full.streamStates);
    });

    it("chained resume: 5 + 8 + 7 unequal pages reconstruct the full window", () => {
        const full = MotelyJamlyzer.analyzeSeedsPaged(jaml.oneSeed, 20)[0];
        const a = MotelyJamlyzer.analyzeSeedsPaged(jaml.oneSeed, 5)[0];
        const b = MotelyJamlyzer.resumeSeeds(jaml.oneSeed, a.streamStates, 8)[0];
        const c = MotelyJamlyzer.resumeSeeds(jaml.oneSeed, b.streamStates, 7)[0];

        assert.equal(a.streamStates.rollOffset, 5);
        assert.equal(b.streamStates.rollOffset, 13);
        assert.equal(c.streamStates.rollOffset, 20);
        assert.deepEqual(c.streamStates, full.streamStates);

        assert.deepEqual(
            [...a.events.misprint, ...b.events.misprint, ...c.events.misprint],
            [...full.events.misprint], "misprint across 3 pages");
        assert.deepEqual(
            [...a.antes[7].pulls.emperorTarots, ...b.antes[7].pulls.emperorTarots, ...c.antes[7].pulls.emperorTarots],
            [...full.antes[7].pulls.emperorTarots], "ante8 emperorTarots across 3 pages");
    });

    it("multi-seed resume throws — the state bag is seed-specific", () => {
        const bag = MotelyJamlyzer.analyzeSeedsPaged(jaml.oneSeed, 5)[0].streamStates;
        assert.throws(() => MotelyJamlyzer.resumeSeeds(jaml.seeds, bag, 5));
    });

    it("scores a seed by JAMLyzer — real, discriminating score", () => {
        // Learn AAAAAAAA's real ante-1 voucher, then score by it: AAAAAAAA has it (score 1),
        // BBBBBBBB doesn't (score 0). Proves the score reflects the seed, not a constant.
        const [a] = MotelyJamlyzer.analyzeSeeds("name: t\ndeck: Red\nstake: White\nseeds: [AAAAAAAA]\n");
        const voucherName = MotelyVoucher[a.antes[0].voucher];
        const yaml = `name: t
deck: Red
stake: White
seeds: [AAAAAAAA, BBBBBBBB]
should:
  - voucher: ${voucherName}
    antes: [1]
    score: 1
`;
        const bySeed = Object.fromEntries(MotelyJamlyzer.analyzeSeeds(yaml).map((r) => [r.seed, r.score]));
        assert.equal(bySeed.AAAAAAAA, 1, "seed that has the voucher scores 1");
        assert.equal(bySeed.BBBBBBBB, 0, "seed that lacks it scores 0");
    });
});
