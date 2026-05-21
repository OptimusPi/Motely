import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { jaml } from "./fixtures.mjs";

const { Motely } = harness;

describe("search events", () => {
    it("wasm-load onSeedMatch, onScoredResult, onProgress fire with documented shapes", async () => {
        const seeds = ["AAAAAAAA", "BBBBBBBB"];
        const matches = [];
        const scored = [];
        const progress = [];

        const onM = (s) => matches.push(s);
        const onS = (r) => scored.push(r);
        const onP = (p) => progress.push(p);

        Motely.onScoredResult.subscribe(onS);
        Motely.onProgress.subscribe(onP);
        try {
            const search = Motely.fromJaml(jaml.scoring)
                .withListSearch(seeds, seeds.length)
                .withThreadCount(1)
                .withProgressReportIntervalMs(0n)
                .start();
            await search.waitForCompletionAsync();
        } finally {
            Motely.onScoredResult.unsubscribe(onS);
            Motely.onProgress.unsubscribe(onP);
        }

        Motely.onSeedMatch.subscribe(onM);
        try {
            const mustSearch = Motely.fromJaml(jaml.anyMust)
                .withListSearch(seeds, seeds.length)
                .withThreadCount(1)
                .start();
            await mustSearch.waitForCompletionAsync();
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

    it("delivers to handlers subscribed AFTER the settings builder is created", async () => {
        // Callbacks read the Motely.* events at invoke time, not at fromJaml() time.
        // Subscribing after building the search must still deliver — otherwise the only
        // safe order is subscribe-before-build, an undocumented trap. All three are wired
        // by the same code path, so a single missing-wire regression breaks all of them.
        const seeds = ["AAAAAAAA", "BBBBBBBB"];

        const scored = [];
        const progress = [];
        const onS = (r) => scored.push(r);
        const onP = (p) => progress.push(p);

        const scoringSearch = Motely.fromJaml(jaml.scoring)
            .withListSearch(seeds, seeds.length)
            .withThreadCount(1)
            .withProgressReportIntervalMs(0n);

        Motely.onScoredResult.subscribe(onS);
        Motely.onProgress.subscribe(onP);
        try {
            await scoringSearch.start().waitForCompletionAsync();
        } finally {
            Motely.onScoredResult.unsubscribe(onS);
            Motely.onProgress.unsubscribe(onP);
        }

        const matches = [];
        const onM = (s) => matches.push(s);

        const mustSearch = Motely.fromJaml(jaml.anyMust)
            .withListSearch(seeds, seeds.length)
            .withThreadCount(1);

        Motely.onSeedMatch.subscribe(onM);
        try {
            await mustSearch.start().waitForCompletionAsync();
        } finally {
            Motely.onSeedMatch.unsubscribe(onM);
        }

        assert.ok(scored.length > 0, "onScoredResult must fire for a handler subscribed after fromJaml()");
        assert.ok(progress.length > 0, "onProgress must fire for a handler subscribed after fromJaml()");
        assert.ok(matches.length > 0, "onSeedMatch must fire for a handler subscribed after fromJaml()");
    });
});
