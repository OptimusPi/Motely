/**
 * Pins the JS-side parity with AnalyzerUnitTests.TestSeedRouter_CapturesSingleSearchContext.
 * Motely.seedContext returns a real MotelySingleSearchContext instance via Bootsharp instance
 * binding — same shape the C# `var ctx = router.Instance();` test uses. If this test goes red,
 * something in Bootsharp's instance-binding pipeline regressed or someone deleted the export.
 */
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";

const { Motely, MotelyDeck, MotelyStake } = harness;

describe("MotelySingleSearchContext instance binding", () => {
    it("seedContext returns a live JS proxy whose getSeed() round-trips the seed", () => {
        const ctx = Motely.seedContext("1AAAAAAA", MotelyDeck.Red, MotelyStake.White);
        assert.ok(ctx, "seedContext returned null/undefined");
        assert.equal(typeof ctx.getSeed, "function", "ctx.getSeed missing — instance binding broken");
        assert.equal(ctx.getSeed(), "1AAAAAAA");
    });

    it("deck and stake are readable as proxy properties", () => {
        const ctx = Motely.seedContext("1AAAAAAA", MotelyDeck.Red, MotelyStake.White);
        assert.equal(ctx.deck, MotelyDeck.Red);
        assert.equal(ctx.stake, MotelyStake.White);
    });

    it("survives a second call with a different seed (router lifecycle)", () => {
        const a = Motely.seedContext("1AAAAAAA", MotelyDeck.Red, MotelyStake.White);
        assert.equal(a.getSeed(), "1AAAAAAA");
        const b = Motely.seedContext("ALEEB", MotelyDeck.Red, MotelyStake.White);
        assert.equal(b.getSeed(), "ALEEB");
    });
});
