import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";

const { Motely } = harness;

// Real-match tests, not smoke tests.
//
// The rest of the WASM JAML suite only proves a clause "compiles and runs" — it
// never asserts the engine matched the RIGHT seed. That lets the matcher break
// silently for whole major versions while every test stays green.
//
// These pin against C#-verified ground truth: Motely.Tests/seeds/KK1XD111.verified.txt
// proves seed KK1XD111 (deck Ghost, stake Black) has Voucher: Observatory at ANTE 5.
// If the WASM matcher regresses, these go red.

const TARGET = "KK1XD111";
const DECOYS = ["AAAAAAAA", "BBBBBBBB"];

function search(yaml, seeds) {
    const cfg = Motely.parseJaml(yaml);
    cfg.seeds = seeds;
    const r = Motely.runSeedListSearch(cfg);
    assert.equal(r.isCompleted, true, "search should complete");
    assert.equal(
        Number(r.totalSeedsSearched),
        seeds.length,
        "should have evaluated every listed seed",
    );
    return r;
}

describe("real match — KK1XD111 voucher Observatory (Ghost/Black, ante 5)", () => {
    it("matches the target seed and rejects the decoys", () => {
        const r = search(
            `deck: Ghost
stake: Black
must:
  - voucher: Observatory
    antes: [5]
`,
            [TARGET, ...DECOYS],
        );
        assert.equal(r.matchingSeeds, 1n, "exactly the target should match");
    });

    it("rejects the target at the wrong ante — proves the filter discriminates", () => {
        const r = search(
            `deck: Ghost
stake: Black
must:
  - voucher: Observatory
    antes: [1]
`,
            [TARGET],
        );
        assert.equal(r.matchingSeeds, 0n, "Observatory is ante 5, not ante 1");
    });
});
