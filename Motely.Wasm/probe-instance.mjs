// Real single-seed probe for ALEEB — no phantom glue (createSeedRouter /
// createStreamCursor / MotelyStreamKind are gone; they only ever imitated what
// Motely already is). Uses the proven API: parseJaml → jamlyzer (analyze what
// the seed contains) + runSeedListSearch (search it against a filter).
import { readFile } from "node:fs/promises";
import { fileURLToPath, pathToFileURL } from "node:url";
import path from "node:path";

const pkgDir = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    "../motely-wasm",
);
const wasmBytes = await readFile(path.join(pkgDir, "bin/motely-wasm.wasm"));

const bootsharp = (await import(pathToFileURL(path.join(pkgDir, "dist/index.mjs")).href)).default;
const { Motely } = await import(pathToFileURL(path.join(pkgDir, "dist/generated/index.g.mjs")).href);

Motely.reportWasmError = (m) => console.error("[WASM ERROR]", m);

await bootsharp.boot({
    wasm: wasmBytes.buffer.slice(
        wasmBytes.byteOffset,
        wasmBytes.byteOffset + wasmBytes.byteLength,
    ),
});

const SEED = "ALEEB";

// 1) ANALYZE — what does ALEEB actually contain? (jamlyzer walks the real streams)
const analyzeCfg = Motely.parseJaml("must:\n  - joker: AnyJoker\n");
analyzeCfg.seeds = [SEED];
const analysis = Motely.jamlyzer(analyzeCfg);

console.log("=== ALEEB analysis ===");
if (analysis.error) {
    console.log("  error:", analysis.error);
} else {
    const seed = analysis.seeds?.[0];
    const ante1 = seed?.analysis?.antes?.[0];
    console.log("  seed       :", seed?.seed);
    console.log("  ante 1 boss:", ante1?.boss);
    console.log("  shop queue :", ante1?.shopQueue?.slice(0, 6));
    console.log("  packs      :", ante1?.packs);
}

// 2) SEARCH — does ALEEB match this filter? (runSeedListSearch over the one seed)
const searchCfg = Motely.parseJaml(
    "must:\n  - joker: Blueprint\n    antes: [1, 2, 3, 4, 5, 6, 7, 8]\n",
);
searchCfg.seeds = [SEED];
const r = Motely.runSeedListSearch(searchCfg);

console.log("=== ALEEB search (must: Blueprint, antes 1-8) ===");
console.log("  completed :", r.isCompleted);
console.log("  searched  :", Number(r.totalSeedsSearched));
console.log("  matched   :", Number(r.matchingSeeds));
console.log(r.isCompleted ? "ALEEB_OK" : "ALEEB_BAD");
