import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { voucherSearch } from "./fixtures.mjs";

const { MotelyJaml, MotelySearch, MotelyJamlyzer, MotelyVoucher, MotelyUtilities } = harness;

const parse = (text) => MotelyJaml.fromJaml(text);

/** Ante-1 voucher name for a seed — discriminating filter input. */
function ante1Voucher(seed) {
    const [a] = MotelyJamlyzer.analyzeSeeds(
        parse(`name: t\ndeck: Red\nstake: White\nseeds: [${seed}]\n`)
    );
    return MotelyVoucher[a.antes[0].voucher];
}

// Proof = the engine finds a known seed. Shape-regex is not proof.
describe("MotelySearch — list / sequential / collect", () => {
    it("searchList keeps only the seed with the analyzed ante-1 voucher", async () => {
        const voucherName = ante1Voucher("AAAAAAAA");

        let lastProgress = null;
        const onP = (p) => {
            lastProgress = p;
        };
        MotelySearch.onProgress.subscribe(onP);
        let results;
        try {
            results = await MotelySearch.searchList(
                parse(voucherSearch(voucherName, ["AAAAAAAA", "BBBBBBBB"]))
            );
        } finally {
            MotelySearch.onProgress.unsubscribe(onP);
        }

        assert.deepEqual(
            results.map((r) => r.seed),
            ["AAAAAAAA"]
        );
        assert.ok(lastProgress, "progress fired");
        assert.equal(Number(lastProgress.seedsSearched), 2);
    });

    it("searchRandom walks exactly `count` seeds", async () => {
        let progress = null;
        const onP = (p) => {
            progress = p;
        };
        MotelySearch.onProgress.subscribe(onP);
        let results;
        try {
            results = await MotelySearch.searchRandom(
                parse(voucherSearch(MotelyVoucher[0], ["AAAAAAAA"])),
                8
            );
        } finally {
            MotelySearch.onProgress.unsubscribe(onP);
        }
        assert.ok(Array.isArray(results));
        assert.ok(progress, "progress fired");
        assert.equal(Number(progress.seedsSearched), 8, "searched exactly the requested count");
    });

    // Sequential batch [0,1) bc=1 = 35 seeds starting at 11111111.
    // 11111111's ante-1 voucher is DirectorsCut; that batch also hits V1111111.
    it("searchSequential finds 11111111 for its ante-1 voucher", async () => {
        const seed = MotelyUtilities.searchIndexToSeed(0n, 8);
        assert.equal(seed, "11111111");
        const voucherName = ante1Voucher(seed);
        assert.equal(voucherName, "DirectorsCut");

        const filter = `name: t
deck: Red
stake: White
must:
  - voucher: ${voucherName}
    antes: [1]
`;
        let progress = null;
        const onP = (p) => {
            progress = p;
        };
        MotelySearch.onProgress.subscribe(onP);
        let results;
        try {
            results = await MotelySearch.searchSequential(parse(filter), 0n, 1n, 1);
        } finally {
            MotelySearch.onProgress.unsubscribe(onP);
        }

        assert.equal(Number(progress.seedsSearched), 35);
        assert.deepEqual(
            results.map((r) => r.seed).sort(),
            ["11111111", "V1111111"]
        );
    });

    // CLI --collect N: aesthetics first. joker:Any fills on length-1 palindromes.
    it("collect(config, N) finds aesthetic seed 1", async () => {
        const config = parse(`name: t
deck: Red
stake: White
must:
  - joker: Any
`);
        const results = await MotelySearch.collect(config, 5n);
        const seeds = results.map((r) => r.seed);
        assert.ok(seeds.includes("1"), `expected aesthetic seed "1", got ${JSON.stringify(seeds)}`);
        assert.ok(results.length <= 64, `collect(5) overshot badly: ${results.length}`);
    });

    it("collectSequential finds 11111111 for DirectorsCut in batch 0", async () => {
        const seed = "11111111";
        const voucherName = ante1Voucher(seed);
        const config = parse(`name: t
deck: Red
stake: White
must:
  - voucher: ${voucherName}
    antes: [1]
`);
        const results = await MotelySearch.collectSequential(config, 5n, 0n, 1n, 1);
        const seeds = results.map((r) => r.seed);
        assert.ok(seeds.includes(seed), `expected ${seed}, got ${JSON.stringify(seeds)}`);
        assert.ok(results.length <= 32, `collectSequential(5) overshot: ${results.length}`);
    });

    it("findOne is collect(config, 1) and finds aesthetic seed 1", async () => {
        const config = parse(`name: t
deck: Red
stake: White
must:
  - joker: Any
`);
        const results = await MotelySearch.findOne(config);
        const seeds = results.map((r) => r.seed);
        assert.ok(seeds.includes("1"), `expected aesthetic seed "1", got ${JSON.stringify(seeds)}`);
    });
});
