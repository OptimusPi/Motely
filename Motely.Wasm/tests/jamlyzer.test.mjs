import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { jaml } from "./fixtures.mjs";

const { MotelyJamlyzer, MotelyVoucher } = harness;

describe("MotelyJamlyzer", () => {
    it("analyzeSeeds returns one result per seed, each with 8 antes", () => {
        const results = MotelyJamlyzer.analyzeSeeds(jaml.seeds);
        assert.equal(results.length, 2);
        assert.equal(results[0].seed, "UNITTEST");
        assert.equal(results[1].seed, "ALEEB");
        assert.equal(results[0].antes.length, 8);
    });

    it("each result carries the resumable stream-state bag", () => {
        const [first] = MotelyJamlyzer.analyzeSeeds(jaml.seeds);
        assert.ok(first.streamStates, "streamStates present");
        assert.equal(typeof first.streamStates.rollOffset, "number");
    });

    it("resume from the state bag — page1 ++ page2 reconstructs the full window exactly", () => {
        // One uninterrupted window of 20.
        const full = MotelyJamlyzer.analyzeSeedsPaged(jaml.oneSeed, 20)[0];

        // First 10, then resume from the returned bag for 10 more.
        const p1 = MotelyJamlyzer.analyzeSeedsPaged(jaml.oneSeed, 10)[0];
        const p2 = MotelyJamlyzer.resumeSeeds(jaml.oneSeed, p1.streamStates, 10)[0];

        // Offsets advance 0 -> 10 -> 20.
        assert.equal(p1.streamStates.rollOffset, 10);
        assert.equal(p2.streamStates.rollOffset, 20);

        // Stitched event rolls equal the full window — no re-roll, no skip, no drift.
        assert.deepEqual(
            [...p1.events.misprint, ...p2.events.misprint],
            [...full.events.misprint],
            "misprint: page1 ++ page2 == full",
        );
        assert.deepEqual(
            [...p1.events.wheelOfFortune, ...p2.events.wheelOfFortune],
            [...full.events.wheelOfFortune],
            "wheelOfFortune: page1 ++ page2 == full",
        );

        // And a composite (offset-replay) stream, ante 1 Emperor tarots, stitches the same way.
        assert.deepEqual(
            [...p1.antes[0].pulls.emperorTarots, ...p2.antes[0].pulls.emperorTarots],
            [...full.antes[0].pulls.emperorTarots],
            "ante1 emperorTarots: page1 ++ page2 == full",
        );

        // The stitched end-state lands exactly on the full window's end-state.
        assert.deepEqual(p2.streamStates, full.streamStates);
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
