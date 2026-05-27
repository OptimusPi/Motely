import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";

const { Motely, MotelyDeck, MotelyStake, MotelyBossBlind } = harness;

const seedRouterReady = typeof Motely.createSeedRouter === "function";

describe("seed router", {
    skip: seedRouterReady
        ? false
        : "Motely.createSeedRouter not on WASM export surface yet",
}, () => {
    it("does not expose instance() — context stays inside WASM", () => {
        const router = Motely.createSeedRouter(
            "AAAAAAAA",
            MotelyDeck.Red,
            MotelyStake.White
        );
        try {
            assert.equal(router.instance, undefined);
        } finally {
            router.dispose();
        }
    });

    it("captures single search context for seed", () => {
        const router = Motely.createSeedRouter(
            "1AAAAAAA",
            MotelyDeck.Red,
            MotelyStake.White
        );
        try {
            const ctx = router.getContext();
            assert.equal(ctx.getSeed(), "1AAAAAAA");
        } finally {
            router.dispose();
        }
    });
});
