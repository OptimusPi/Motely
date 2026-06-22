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

    it("searchRandom walks exactly `count` seeds", async () => {
        let progress = null;
        const onP = (p) => { progress = p; };
        MotelySearch.onProgress.subscribe(onP);
        try {
            await MotelySearch.searchRandom(voucherSearch(MotelyVoucher[0], ["AAAAAAAA"]), 8);
        } finally {
            MotelySearch.onProgress.unsubscribe(onP);
        }
        assert.ok(progress, "progress fired");
        assert.equal(Number(progress.seedsSearched), 8, "searched exactly the requested count");
    });

    // Sequential = brute-force walk of the seed space. base-35, 8-char seeds: each batch sweeps
    // 35^batchCharacterCount seeds. [0,1) with bc=1 is one batch = exactly 35 seeds — a real,
    // deterministic slice we can pin a count to (not a "completes" no-op).
    it("searchSequential walks an exact, deterministic 35-seed slice and emits real seeds", async () => {
        const filter = voucherSearch(MotelyVoucher[0], ["AAAAAAAA"]); // seed list is ignored in sequential mode
        const run = async () => {
            const found = [];
            let progress = null;
            const onR = (r) => found.push(r.seed);
            const onM = (s) => found.push(s);
            const onP = (p) => { progress = p; };
            MotelySearch.onScoredResult.subscribe(onR);
            MotelySearch.onSeedMatch.subscribe(onM);
            MotelySearch.onProgress.subscribe(onP);
            // startBatchIndex/endBatchIndex are C# `long` -> BigInt across interop; batch count is `int`.
            try { await MotelySearch.searchSequential(filter, 0n, 1n, 1); }
            finally {
                MotelySearch.onScoredResult.unsubscribe(onR);
                MotelySearch.onSeedMatch.unsubscribe(onM);
                MotelySearch.onProgress.unsubscribe(onP);
            }
            return { count: Number(progress.seedsSearched), found };
        };

        const a = await run();
        const b = await run();
        assert.equal(a.count, 35, "one batch (bc=1) sweeps the 35-char alphabet");
        assert.equal(a.count, b.count, "the walk is deterministic");
        for (const s of a.found)
            assert.match(s, /^[1-9A-Z]{8}$/, "emits real base-35 8-char seeds");
    });
});
