import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { jaml } from "./fixtures.mjs";

const { Motely, MotelyDeck, MotelyStake } = harness;

const seedRouterReady = typeof Motely.createSeedRouter === "function";

describe("single search context", {
    skip: seedRouterReady
        ? false
        : "Motely.createSeedRouter not on WASM export surface yet",
}, () => {
    it("drives shop stream matching analyzer", () => {
        const seed = "UNITTEST";
        const router = Motely.createSeedRouter(seed, MotelyDeck.Red, MotelyStake.White);
        try {
            const ctx = router.getContext();
            const shopStream = ctx.createShopItemStreamClass(1, 0, 0, false);
            const fromContext = [];
            for (let i = 0; i < 5; i++) {
                fromContext.push(ctx.getNextShopItem(shopStream));
            }

            const analysisResult = Motely.analyzeJamlSeeds(jaml.anyMust, [seed]);
            assert.ok(analysisResult.error == null);
            const fromAnalyzer = [];
            const shopQueue = analysisResult.seeds[0].analysis.antes[0].shopQueue;
            for (let i = 0; i < 5; i++) {
                fromAnalyzer.push(shopQueue[i].item.value);
            }

            assert.deepEqual(fromContext, fromAnalyzer);
        } finally {
            router.dispose();
        }
    });

    it("drives generic PRNG stream states", () => {
        const seed = "UNITTEST";
        const router = Motely.createSeedRouter(seed, MotelyDeck.Red, MotelyStake.White);
        try {
            const ctx = router.getContext();
            const stream = ctx.createPrngStreamClass("test_key", false);
            
            const firstRand = ctx.getNextRandom(stream);
            assert.equal(typeof firstRand, "number");
            assert.ok(firstRand >= 0 && firstRand <= 1);
            
            const state = ctx.getPrngState(stream);
            assert.equal(typeof state, "number");
            
            const secondRand = ctx.getNextRandom(stream);
            assert.notEqual(firstRand, secondRand);
            
            // Setting state back should produce the same second random
            ctx.setPrngState(stream, state);
            const thirdRand = ctx.getNextRandom(stream);
            assert.equal(secondRand, thirdRand);
        } finally {
            router.dispose();
        }
    });
});
