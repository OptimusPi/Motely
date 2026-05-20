import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { jaml, probeSeeds } from "./fixtures.mjs";

const { Motely, MotelyItemType } = harness;

async function withEvalJimmolate(fn) {
    const prev = Motely.evalJimmolate;
    try {
        await fn();
    } finally {
        Motely.evalJimmolate = prev ?? (() => true);
    }
}

describe("jimmolate", () => {
    it("JS predicate filters list search results", async () => {
        const seeds = [
            "MAAAAAAA",
            "MBBBBBBB",
            "XCCCCCCC",
            "MADDDDDD",
            "XEEEEEEE",
            "MAFFFFFF",
            "XGGGGGGG",
            "MAHHHHHH",
        ];
        const expectedMatchCount = seeds.filter(
            (s) => s[0] === "M" && s[1] === "A"
        ).length;

        await withEvalJimmolate(async () => {
            const visited = [];
            Motely.evalJimmolate = (seed) => {
                visited.push(seed);
                return seed.length >= 2 && seed[1] === "A";
            };

            const search = Motely.createSearch(jaml.anyMust)
                .withJimmolate()
                .withListSearch(seeds, seeds.length)
                .withThreadCount(1)
                .start();
            await search.waitForCompletionAsync();

            assert.equal(Number(search.matchingSeeds), expectedMatchCount);
            assert.equal(visited.length, seeds.length);
            assert.equal(typeof search.totalSeedsSearched, "bigint");
        });
    });

    it("always-false predicate yields zero matches", async () => {
        const seeds = probeSeeds.slice(0, 2);
        await withEvalJimmolate(async () => {
            Motely.evalJimmolate = () => false;
            const search = Motely.createSearch(jaml.anyMust)
                .withJimmolate()
                .withListSearch(seeds, seeds.length)
                .withThreadCount(1)
                .start();
            await search.waitForCompletionAsync();
            assert.equal(search.matchingSeeds, 0n);
        });
    });

    it("predicate runs only on base survivors", async () => {
        const r = Motely.analyzeJamlSeeds(jaml.anyMust, probeSeeds);
        assert.ok(r.error == null);

        let jokerName = null;
        for (const s of r.seeds ?? []) {
            const item = s.analysis?.antes?.[0]?.packs?.[0]?.items?.[0];
            if (!item) continue;
            const name = MotelyItemType?.[Motely.decodeItemType(item.item.value)];
            if (name) {
                jokerName = name;
                break;
            }
        }
        assert.ok(jokerName);

        const expectedSurvivorCount = r.seeds.filter((s) => {
            const item = s.analysis?.antes?.[0]?.packs?.[0]?.items?.[0];
            return (
                MotelyItemType?.[Motely.decodeItemType(item.item.value)] ===
                jokerName
            );
        }).length;
        assert.ok(expectedSurvivorCount > 0);

        const derivedJaml = `name: t\ndeck: Red\nstake: White\nmust:\n  - joker: ${jokerName}\n    antes: [1]\n    sources:\n      boosterPacks: [0]\n`;

        await withEvalJimmolate(async () => {
            const visited = [];
            Motely.evalJimmolate = (seed) => {
                visited.push(seed);
                return true;
            };
            const search = Motely.createSearch(derivedJaml)
                .withJimmolate()
                .withListSearch(probeSeeds, probeSeeds.length)
                .withThreadCount(1)
                .start();
            await search.waitForCompletionAsync();
            assert.equal(visited.length, expectedSurvivorCount);
            assert.equal(search.matchingSeeds, BigInt(expectedSurvivorCount));
        });
    });

    it("sequential search respects predicate", async () => {
        await withEvalJimmolate(async () => {
            const visited = [];
            Motely.evalJimmolate = (seed) => {
                visited.push(seed);
                return true;
            };
            const search = Motely.createSearch(jaml.anyMust)
                .withSequentialSearch()
                .withBatchCharacterCount(1)
                .withStartBatchIndex(0n)
                .withEndBatchIndex(0n)
                .withJimmolate()
                .withThreadCount(1)
                .start();
            await search.waitForCompletionAsync();
            assert.equal(search.isCompleted, true);
            assert.equal(typeof search.matchingSeeds, "bigint");
            assert.equal(visited.length, Number(search.matchingSeeds));
        });
    });
});
