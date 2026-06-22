import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { jaml } from "./fixtures.mjs";

const { MotelySearch, MotelyJamlyzer, MotelyVoucher, MotelyDeck, MotelyStake, Jimmolate } =
    harness;

// Two seeds, both surviving the JAML phase — jimmolate is the only gate that can drop them.
// JamlSearchBuilder requires at least one clause, so we add a non-blocking `should` that only
// scores seeds without rejecting any (mirrors the PassthroughFilterDesc + JimmolateFilter combo
// in JimmolateFilterTests.cs, where the passthrough passes all seeds to the jimmolate hook).
const twoSeeds =
    "name: t\ndeck: Red\nstake: White\nseeds: [UNITTEST, ALEEBOOO]\n" +
    "should:\n  - voucher: Overstock Plus\n    antes: [1]\n    score: 1\n";

async function found(yaml) {
    const seeds = [];
    const on = (r) => seeds.push(r.seed);
    MotelySearch.onScoredResult.subscribe(on);
    try { await MotelySearch.searchList(yaml); }
    finally { MotelySearch.onScoredResult.unsubscribe(on); }
    return seeds.sort();
}

function withFinder(fn) {
    harness.setFinder(fn);
    Jimmolate.enabled = true;
    return () => { Jimmolate.enabled = false; harness.resetFinder(); };
}

describe("Jimmolate seed finder", () => {
    it("accept-all: every seed passes through", async () => {
        const teardown = withFinder(() => true);
        try { assert.deepEqual(await found(twoSeeds), ["ALEEBOOO", "UNITTEST"]); }
        finally { teardown(); }
    });

    it("reject-all: no seeds pass through", async () => {
        const teardown = withFinder(() => false);
        try { assert.deepEqual(await found(twoSeeds), []); }
        finally { teardown(); }
    });

    // Mirrors JimmolateFilterTests.Jimmolate_PredicateBoolDrivesFiltering_KeepsOnlyTargetSeed.
    it("predicate bool drives filtering: keep only UNITTEST", async () => {
        const teardown = withFinder((seed) => seed === "UNITTEST");
        try { assert.deepEqual(await found(twoSeeds), ["UNITTEST"]); }
        finally { teardown(); }
    });

    // JS side can't get a live search context (Bootsharp marshals deck+stake, not a C# ref struct),
    // but it does receive the JAML's deck and stake — verify they're passed correctly.
    it("finder receives the correct deck and stake from the JAML config", async () => {
        const seen = [];
        const teardown = withFinder((seed, deck, stake) => { seen.push({ seed, deck, stake }); return true; });
        try {
            await found(twoSeeds);
            assert.equal(seen.length, 2, "finder called once per seed");
            for (const { deck, stake } of seen) {
                assert.equal(deck, MotelyDeck.Red, "deck matches JAML");
                assert.equal(stake, MotelyStake.White, "stake matches JAML");
            }
        } finally { teardown(); }
    });

    // Finder must only be offered seeds that already survived the JAML must-clauses.
    it("finder is offered only JAML-surviving seeds", async () => {
        // Discover AAAAAAAA's ante-1 voucher via the analyzer, then build a must-filter
        // that only AAAAAAAA satisfies — BBBBBBBB should never reach jimmolate.
        const [a] = MotelyJamlyzer.analyzeSeeds(
            "name: t\ndeck: Red\nstake: White\nseeds: [AAAAAAAA]\n"
        );
        const voucherName = MotelyVoucher[a.antes[0].voucher];
        const filter =
            `name: t\ndeck: Red\nstake: White\nseeds: [AAAAAAAA, BBBBBBBB]\nmust:\n` +
            `  - voucher: ${voucherName}\n    antes: [1]\n`;

        const offered = [];
        const teardown = withFinder((seed) => { offered.push(seed); return true; });
        try {
            await found(filter);
            assert.deepEqual(offered, ["AAAAAAAA"], "BBBBBBBB failed the JAML filter; jimmolate never sees it");
        } finally { teardown(); }
    });
});
