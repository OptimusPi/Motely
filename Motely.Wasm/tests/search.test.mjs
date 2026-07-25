import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { voucherSearch } from "./fixtures.mjs";

const { MotelyJaml, MotelySearch, MotelyJamlyzer, MotelyVoucher } = harness;

const parse = (text) => MotelyJaml.fromJaml(text);

// Discriminating find: analyzer voucher on AAAAAAAA; list search keeps that seed, drops BBBBBBBB.
describe("MotelySearch — list / sequential / collect", () => {
    it("finds exactly the seed that has the analyzed ante-1 voucher", async () => {
        const [a] = MotelyJamlyzer.analyzeSeeds(parse("name: t\ndeck: Red\nstake: White\nseeds: [AAAAAAAA]\n"));
        const voucherName = MotelyVoucher[a.antes[0].voucher]; // numeric enum -> name

        let lastProgress = null;
        const onP = (p) => { lastProgress = p; };
        MotelySearch.onProgress.subscribe(onP);
        let results;
        try {
            results = await MotelySearch.searchList(parse(voucherSearch(voucherName, ["AAAAAAAA", "BBBBBBBB"])));
        } finally {
            MotelySearch.onProgress.unsubscribe(onP);
        }

        assert.deepEqual(results.map((r) => r.seed), ["AAAAAAAA"]);
        assert.ok(lastProgress, "progress fired");
        assert.equal(Number(lastProgress.seedsSearched), 2);
    });

    it("searchRandom walks exactly `count` seeds and returns its finds", async () => {
        let progress = null;
        const onP = (p) => { progress = p; };
        MotelySearch.onProgress.subscribe(onP);
        let results;
        try {
            results = await MotelySearch.searchRandom(parse(voucherSearch(MotelyVoucher[0], ["AAAAAAAA"])), 8);
        } finally {
            MotelySearch.onProgress.unsubscribe(onP);
        }
        assert.ok(Array.isArray(results), "the call resolves with the results array");
        assert.ok(progress, "progress fired");
        assert.equal(Number(progress.seedsSearched), 8, "searched exactly the requested count");
    });

    // Sequential: base-35, 8-char. [0,1) with bc=1 = one batch = 35 seeds.
    it("searchSequential walks a deterministic 35-seed slice", async () => {
        const filter = voucherSearch(MotelyVoucher[0], ["AAAAAAAA"]); // seed list is ignored in sequential mode
        const run = async () => {
            const matched = [];
            let progress = null;
            const onM = (s) => matched.push(s);
            const onP = (p) => { progress = p; };
            MotelySearch.onSeedMatch.subscribe(onM);
            MotelySearch.onProgress.subscribe(onP);
            let results;
            // startBatchIndex/endBatchIndex are C# `long` -> BigInt across interop; batch count is `int`.
            try { results = await MotelySearch.searchSequential(parse(filter), 0n, 1n, 1); }
            finally {
                MotelySearch.onSeedMatch.unsubscribe(onM);
                MotelySearch.onProgress.unsubscribe(onP);
            }
            return { count: Number(progress.seedsSearched), matched, results };
        };

        const a = await run();
        const b = await run();
        assert.equal(a.count, 35, "one batch (bc=1) sweeps the 35-char alphabet");
        assert.equal(a.count, b.count, "the walk is deterministic");
        for (const s of a.matched)
            assert.match(s, /^[1-9A-Z]{8}$/);
        for (const r of a.results)
            assert.match(r.seed, /^[1-9A-Z]{8}$/);
    });

    // CLI --collect N twin: JamlSearchBuilder + StopAfter(N). Any-joker hits in batch 0.
    it("collect stops near N matches (CLI --collect N twin)", async () => {
        const config = parse(`name: t
deck: Red
stake: White
must:
  - joker: Any
`);
        const results = await MotelySearch.collect(config, 5n, 0n, 1n, 1);
        assert.ok(results.length >= 1, "collect must hit at least one seed in batch 0");
        // Thread count is 1 in WASM; overshoot is one SIMD batch (8), not thousands.
        assert.ok(results.length <= 32, `collect(5) overshot badly: ${results.length}`);
        assert.match(results[0].seed, /^[1-9A-Z]{8}$/);
    });

    it("findOne is collect(1)", async () => {
        const config = parse(`name: t
deck: Red
stake: White
must:
  - joker: Any
`);
        const results = await MotelySearch.findOne(config, 0n, 1n, 1);
        assert.ok(results.length >= 1, "findOne must hit at least one seed in batch 0");
        assert.match(results[0].seed, /^[1-9A-Z]{8}$/);
    });
});
