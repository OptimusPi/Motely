import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { voucherSearch } from "./fixtures.mjs";

const { MotelySearch, MotelyJamlyzer, MotelyVoucher, Jimmolate } = harness;

// Jimmolate = a seed finder you write in JS, slotted into the engine's filter chain. When Enabled,
// every seed surviving the JAML filters is offered to findSeed(seed, deck, stake); returning false
// drops it. The finder import is bound once in harness.mjs before boot; setFinder swaps the delegate.
describe("Jimmolate seed finder — real gating", () => {
    // A real filter that matches exactly AAAAAAAA (its ante-1 voucher), not BBBBBBBB.
    const [a] = MotelyJamlyzer.analyzeSeeds("name: t\ndeck: Red\nstake: White\nseeds: [AAAAAAAA]\n");
    const filter = voucherSearch(MotelyVoucher[a.antes[0].voucher], ["AAAAAAAA", "BBBBBBBB"]);

    async function found(yaml) {
        const seeds = [];
        const on = (r) => seeds.push(r.seed);
        MotelySearch.onScoredResult.subscribe(on);
        try { await MotelySearch.searchList(yaml); }
        finally { MotelySearch.onScoredResult.unsubscribe(on); }
        return seeds;
    }

    it("baseline (no jimmolate): finds AAAAAAAA", async () => {
        assert.deepEqual(await found(filter), ["AAAAAAAA"]);
    });

    it("enabled: offered the surviving seed; reject -> none, accept -> it", async () => {
        const offered = [];
        harness.setFinder((seed) => { offered.push(seed); return false; });
        Jimmolate.enabled = true;
        try {
            assert.deepEqual(await found(filter), [], "reject all -> nothing kept");
            assert.deepEqual(offered, ["AAAAAAAA"], "finder is offered the surviving seed");

            harness.setFinder(() => true);
            assert.deepEqual(await found(filter), ["AAAAAAAA"], "accept -> the real seed is kept");
        } finally {
            Jimmolate.enabled = false;
            harness.resetFinder();
        }
    });
});
