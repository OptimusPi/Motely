import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const testsDir = dirname(fileURLToPath(import.meta.url));
const entryPath = process.env.MOTELY_WASM_ENTRY
    ? resolve(process.env.MOTELY_WASM_ENTRY)
    : resolve(testsDir, "..", "..", "motely-wasm", "dist", "index.mjs");

const visited = [];

async function bootOnce() {
    const { default: bootsharp, Motely } = await import(pathToFileURL(entryPath).href);
    Motely.reportWasmError = (message) => console.error("[WASM ERROR]", message);
    Motely.jimmolateProbe = (seed, _deck, _stake) => {
        if (seed.length > 0 && seed[0] === "M") {
            visited.push(seed);
        }
        return seed.length >= 2 && seed[1] === "A";
    };
    await bootsharp.boot();
    if (bootsharp.getStatus() !== bootsharp.BootStatus.Booted) {
        throw new Error("boot: expected BootStatus.Booted");
    }
    Motely.enableJimmolate();
    return Motely;
}

const Motely = await bootOnce();

describe("individual seed search", () => {
    it("PerkeoObservatory native filter runs list search (SearchIndividualSeeds in C#)", () => {
        const seeds = ["MAAAAAAA", "MBBBBBBB"];
        const r = Motely.runNativeListSearch("PerkeoObservatory", seeds);
        assert.equal(r.isCompleted, true);
        assert.equal(Number(r.totalSeedsSearched), seeds.length);
        assert.equal(typeof r.matchingSeeds, "bigint");
    });

    it("withJimmolate uses JimmolateFilterDesc + JS import probe (same path as xUnit)", () => {
        const seeds = ["MAAAAAAA", "MBBBBBBB", "XCCCCCCC", "MADDDDDD", "MAFFFFFF"];
        visited.length = 0;
        const matches = [];
        const onSeedMatch = (seed) => matches.push(seed);
        Motely.onSeedMatch.subscribe(onSeedMatch);

        let r;
        try {
            r = Motely.runPassthroughListSearch(seeds);
        } finally {
            Motely.onSeedMatch.unsubscribe(onSeedMatch);
        }

        assert.equal(r.isCompleted, true);
        assert.equal(Number(r.matchingSeeds), 3);
        assert.deepEqual(
            matches.sort(),
            ["MAAAAAAA", "MADDDDDD", "MAFFFFFF"].sort()
        );
        assert.equal(visited.length, 4);
        assert.deepEqual(
            visited.sort(),
            ["MAAAAAAA", "MBBBBBBB", "MADDDDDD", "MAFFFFFF"].sort()
        );
    });
});
