// Smoke test for the published motely WASM module (Release artifact, real engine).
// Run: node Motely.Wasm/test/smoke.mjs
import assert from "node:assert/strict";
import bootsharp, { Program, MotelyVoucher, MotelyBossBlind } from "../bin/motely/index.mjs";

await bootsharp.boot();
console.log("booted: .NET", bootsharp.runtime ? "runtime up" : "");

// Real JAML. ALEEB's verified ante-1 fingerprint (seeds/ALEEB.verified.txt): Magic Trick
// voucher + The Window boss. PIROCKS/LOVEYAHB are decoys, same as JimmolateFilterTests.
const yaml = `
name: Wasm Smoke
deck: Red
stake: White
seeds:
  - PIROCKS
  - ALEEB
  - LOVEYAHB
should:
  - voucher: MagicTrick
    antes:
      - 1
    score: 10
`;

// ── loadJaml: real JamlConfig crosses by reference ──
const cfg = Program.loadJaml(yaml);
assert.equal(cfg.name, "Wasm Smoke");
console.log("loadJaml ok:", cfg.name, "seeds:", cfg.seeds);

// ── Jamlyzer: full structured records by value ──
const results = Program.analyze(cfg);
assert.equal(results.length, 3);
const aleeb = results.find(r => r.seed === "ALEEB");
assert.equal(aleeb.antes[0].voucher, MotelyVoucher.MagicTrick);
assert.equal(aleeb.antes[0].boss, MotelyBossBlind.TheWindow);
console.log(
    "jamlyzer ok: ALEEB ante1 voucher =", MotelyVoucher[aleeb.antes[0].voucher],
    "boss =", MotelyBossBlind[aleeb.antes[0].boss],
    "score =", aleeb.score,
    "shopItems =", aleeb.antes[0].shopItems.length
);

// ── analyzeSeed: single-seed pull, e.g. per search match ──
const solo = Program.analyzeSeed(cfg, "ALEEB");
assert.equal(solo.seed, "ALEEB");
assert.equal(solo.antes[0].voucher, MotelyVoucher.MagicTrick);
console.log("analyzeSeed ok");

// ── live config tweak from JS + analyzeNext: scroll event streams, no dupes/skips ──
cfg.seeds = ["ALEEB"];
const first = Program.analyzeSeed(cfg, "ALEEB");
const next = Program.analyzeNext(cfg, first.streamStates);
assert.ok(next.streamStates.rollOffset > first.streamStates.rollOffset,
    `rollOffset should advance: ${first.streamStates.rollOffset} -> ${next.streamStates.rollOffset}`);
console.log("analyzeNext ok: rollOffset", first.streamStates.rollOffset, "->", next.streamStates.rollOffset);

// ── search with a JS-authored Jimmolate predicate running IN-ENGINE ──
// The JAML only *scores* MagicTrick (should:), it filters nothing. The JS predicate is the
// filter: it queries the live MotelySingleSearchContext mid-search and keeps MagicTrick seeds.
cfg.seeds = ["PIROCKS", "ALEEB", "LOVEYAHB"];
const probed = [];
const settings = Program.createSearch(cfg, 0, ctx => {
    probed.push(ctx.getSeed()); // live in-engine query, per seed
    return ctx.getAnteFirstVoucher(1) === MotelyVoucher.MagicTrick;
});

const matched = [];
settings
    .withQuietMode(true)
    .withScoredResultCallback(result => matched.push(`${result.seed}:${result.score}`));

const search = settings.start();
await search.waitForCompletionAsync();

assert.deepEqual([...probed].sort(), ["ALEEB", "LOVEYAHB", "PIROCKS"]);
assert.deepEqual(matched, ["ALEEB:10"]);
assert.equal(search.matchingSeeds, 1n);
console.log("jimmolate ok: probed", probed.length, "seeds in-engine, matched:", matched);

console.log("\nALL SMOKE CHECKS PASSED");
