import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { jaml } from "./fixtures.mjs";

const { Motely } = harness;

describe("search", () => {
    it("list search completes and matches joker:Any", () => {
        const cfg = Motely.fromYaml(jaml.anyMust);
        cfg.seeds = ["AAAAAAAA", "BBBBBBBB"];
        const r = Motely.runSeedListSearch(cfg);
        assert.equal(r.isCompleted, true);
        assert.equal(Number(r.totalSeedsSearched), 2);
        assert.ok(Number(r.matchingSeeds) >= 1);
    });

    it("sequential search runs a bounded batch range", () => {
        const cfg = Motely.fromYaml(jaml.anyMust);
        const r = Motely.runSequentialSearch(cfg, 0n, 1n, 2, 0n);
        assert.equal(r.isCompleted, true);
        assert.ok(r.matchingSeeds >= 1n);
    });
});
