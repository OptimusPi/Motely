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
