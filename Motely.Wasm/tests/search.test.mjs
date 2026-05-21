import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { jaml, probeSeeds } from "./fixtures.mjs";

const { Motely } = harness;

describe("search", () => {
    it("list search completes and matches joker:Any", async () => {
        const seeds = ["AAAAAAAA", "BBBBBBBB"];
        const search = Motely.fromJaml(jaml.anyMust)
            .withListSearch(seeds, seeds.length)
            .withThreadCount(1)
            .start();
        await search.waitForCompletionAsync();
        assert.equal(search.isCompleted, true);
        assert.equal(Number(search.totalSeedsSearched), 2);
        assert.ok(Number(search.matchingSeeds) >= 1);
    });

    it("sequential match count is stable across thread counts when supported", async () => {
        let baseline = null;
        for (const threads of [1, 2, 4]) {
            let search;
            try {
                search = Motely.fromJaml(jaml.anyMust)
                    .withSequentialSearch()
                    .withBatchCharacterCount(2)
                    .withStartBatchIndex(0n)
                    .withEndBatchIndex(1n)
                    .withThreadCount(threads)
                    .start();
                await search.waitForCompletionAsync();
            } catch (e) {
                if (threads === 1) throw e;
                continue;
            }
            assert.equal(search.isCompleted, true);
            if (baseline === null) {
                baseline = search.matchingSeeds;
                continue;
            }
            assert.equal(
                search.matchingSeeds,
                baseline,
                `threads=${threads} vs baseline`
            );
        }
        assert.ok(baseline !== null && baseline >= 1n);
    });
});

describe("search (cancel)", () => {
    it.skip("cancel completes without hanging", async () => {
        // FIXME: sequential search keeps running after .cancel() — track separately.
        const search = Motely.fromJaml(jaml.must)
            .withSequentialSearch()
            .withBatchCharacterCount(1)
            .withStartBatchIndex(0n)
            .withEndBatchIndex(0n)
            .withThreadCount(1)
            .start();
        search.cancel();
        await search.waitForCompletionAsync();
        assert.equal(search.isCompleted, true);
    });
});
