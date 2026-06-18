import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { jaml } from "./fixtures.mjs";

const { Motely } = harness;

// Jimmolate = the OG Immolate `filter(seed) => keep?` model: JS assigns
// `jimmolatePredicate`, flips `jimmolateEnabled`, and every SCORED seed is
// offered to the predicate — onScoredResult fires only for kept seeds.
// Note: only seeds that actually score are reported at all, so assertions
// run against a disabled-mode baseline instead of hardcoded counts.
describe("jimmolate predicate", () => {
    const SEEDS = ["MOTELY77", "JAML2026", "AAAAAAAA", "BBBBBBBB"];

    function runScored(cfg) {
        const scored = [];
        const onS = (r) => scored.push(r);
        Motely.onScoredResult.subscribe(onS);
        try {
            cfg.seeds = SEEDS;
            Motely.runSeedListSearch(cfg);
        } finally {
            Motely.onScoredResult.unsubscribe(onS);
        }
        return scored;
    }

    Motely.jimmolateEnabled = false;
    const baseline = runScored(Motely.parseJaml(jaml.scoring));

    it("disabled baseline produces scored seeds to filter", () => {
        assert.ok(baseline.length >= 1, "need at least one scoring seed");
    });

    it("enabled: predicate sees every scored seed and decides survival", () => {
        const keeper = baseline[0].seed;
        const offered = [];
        Motely.jimmolatePredicate = (result) => {
            offered.push(result.seed);
            return result.seed === keeper;
        };
        Motely.jimmolateEnabled = true;
        try {
            const scored = runScored(Motely.parseJaml(jaml.scoring));
            assert.equal(offered.length, baseline.length, "predicate sees every scored seed");
            assert.equal(scored.length, 1, "only kept seeds are reported");
            assert.equal(scored[0].seed, keeper);
            assert.equal(typeof scored[0].score, "number");
        } finally {
            Motely.jimmolateEnabled = false;
        }
    });

    it("enabled: rejecting everything reports nothing", () => {
        Motely.jimmolatePredicate = () => false;
        Motely.jimmolateEnabled = true;
        try {
            const scored = runScored(Motely.parseJaml(jaml.scoring));
            assert.equal(scored.length, 0);
        } finally {
            Motely.jimmolateEnabled = false;
        }
    });
});
