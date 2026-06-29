import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { resolve, dirname } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

// JS mirror of find-claude22.cs. Jimmolate is one-kernel-per-boot: the predicate is an [Import],
// so it must be BOUND BEFORE boot(). This suite boots its OWN runtime with the finder already in
// place — it can't reuse the shared harness.mjs (which boots at import, before any finder is set).
//
// The find is real: CLAUDE22 is pulled out of decoys by DERIVING its ante-1 voucher (Paint Brush,
// Erratic/White), not by reading its name. ctx.getAnteFirstVoucher(1) runs native, in-engine.

const testsDir = dirname(fileURLToPath(import.meta.url));
const entry = process.env.MOTELY_WASM_ENTRY
    ? resolve(process.env.MOTELY_WASM_ENTRY)
    : resolve(testsDir, "..", "dist", "index.mjs");

const mod = await import(pathToFileURL(entry).href);
const bootsharp = mod.default;
const { MotelyJaml, MotelySearch, Jimmolate, MotelyVoucher } = mod;

const scored = [];
MotelySearch.onScoredResult.subscribe((r) => scored.push(r.seed));

// BIND BEFORE BOOT — the predicate derives the ante-1 voucher (the Immolate model).
Jimmolate.findSeed = (ctx) => ctx.getAnteFirstVoucher(1) === MotelyVoucher.PaintBrush;

await bootsharp.boot();
if (bootsharp.getStatus() !== bootsharp.BootStatus.Booted)
    throw new Error("boot: expected BootStatus.Booted");

describe("Jimmolate — derives, finds CLAUDE22 by its ante-1 voucher", () => {
    it("pulls CLAUDE22 out of decoys by Paint Brush, not by name", async () => {
        scored.length = 0;
        // Non-blocking `should` satisfies JamlSearchBuilder's "≥1 clause" rule and scores every
        // surviving seed; jimmolate is the gate that actually drops the decoys.
        const jaml = MotelyJaml.fromYaml(
            "name: t\ndeck: Erratic\nstake: White\n" +
            "seeds: [DECOY111, CLAUDE22, DECOY222]\n" +
            "should:\n  - voucher: Overstock\n    antes: [1]\n    score: 1\n"
        );
        await MotelySearch.searchList(jaml);
        assert.deepEqual(scored.sort(), ["CLAUDE22"],
            "jimmolate keeps only the seed whose ante-1 voucher is Paint Brush");
    });
});
