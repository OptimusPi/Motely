import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { jaml } from "./fixtures.mjs";

const { Motely } = harness;

describe("search events", () => {
    it("onSeedMatch, onScoredResult, onProgress fire with documented shapes", () => {
        const matches = [];
        const scored = [];
        const progress = [];

        const onM = (s) => matches.push(s);
        const onS = (r) => scored.push(r);
        const onP = (p) => progress.push(p);

        const scoringCfg = Motely.fromJaml(jaml.scoring);
        scoringCfg.seeds = ["AAAAAAAA", "BBBBBBBB"];

        Motely.onScoredResult.subscribe(onS);
        Motely.onProgress.subscribe(onP);
        try {
            Motely.runSeedListSearch(scoringCfg);
        } finally {
            Motely.onScoredResult.unsubscribe(onS);
            Motely.onProgress.unsubscribe(onP);
        }

        const mustCfg = Motely.fromJaml(jaml.anyMust);
        mustCfg.seeds = ["AAAAAAAA", "BBBBBBBB"];
        Motely.onSeedMatch.subscribe(onM);
        try {
            Motely.runSeedListSearch(mustCfg);
        } finally {
            Motely.onSeedMatch.unsubscribe(onM);
        }

        assert.ok(matches.length > 0);
        assert.equal(typeof matches[0], "string");
        assert.ok(matches[0].length > 0);

        assert.ok(scored.length > 0);
        const r = scored[0];
        const talliesOk =
            (r.tallies instanceof Int32Array || Array.isArray(r.tallies)) &&
            r.tallies.length === 2 &&
            typeof r.tallies[0] === "number";
        assert.equal(typeof r.seed, "string");
        assert.equal(typeof r.score, "number");
        assert.ok(talliesOk, `onScoredResult shape: ${JSON.stringify(r)?.slice(0, 100)}`);

        assert.ok(progress.length > 0);
        const p = progress.at(-1);
        assert.equal(typeof p.percentComplete, "number");
        assert.equal(typeof p.seedsPerMillisecond, "number");
        assert.equal(typeof p.seedsSearched, "bigint");
        assert.equal(typeof p.elapsedMilliseconds, "bigint");
    });
});
