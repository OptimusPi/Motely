import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { voucherSearch } from "./fixtures.mjs";

const { MotelySearch, MotelyJamlyzer, MotelyVoucher } = harness;

// A real, discriminating find: learn a deterministic attribute of a real seed from the analyzer,
// then prove the finder finds that seed and rejects one that lacks it. Scored search (a must clause
// attaches a score provider) reports via onScoredResult — a clean { seed, score, tallies }.
describe("MotelySearch — real seed finding", () => {
    it("finds exactly the seed that has the analyzed ante-1 voucher", async () => {
        const [a] = MotelyJamlyzer.analyzeSeeds("name: t\ndeck: Red\nstake: White\nseeds: [AAAAAAAA]\n");
        const voucherName = MotelyVoucher[a.antes[0].voucher]; // numeric enum -> name

        const found = [];
        let lastProgress = null;
        const onR = (r) => found.push(r);
        const onP = (p) => { lastProgress = p; };
        MotelySearch.onScoredResult.subscribe(onR);
        MotelySearch.onProgress.subscribe(onP);
        try {
            const ret = await MotelySearch.searchList(voucherSearch(voucherName, ["AAAAAAAA", "BBBBBBBB"]));
            assert.equal(ret, undefined, "search returns void — results come via callbacks");
        } finally {
            MotelySearch.onScoredResult.unsubscribe(onR);
            MotelySearch.onProgress.unsubscribe(onP);
        }

        assert.deepEqual(found.map((r) => r.seed), ["AAAAAAAA"],
            "finds the seed that really has the voucher, not the one that doesn't");
        assert.ok(lastProgress, "progress fired");
        assert.equal(Number(lastProgress.seedsSearched), 2);
    });

    it("searchRandom completes for a small count", async () => {
        await MotelySearch.searchRandom(voucherSearch(MotelyVoucher[0], ["AAAAAAAA"]), 8);
    });
});
