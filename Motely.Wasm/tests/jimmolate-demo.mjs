// Not a unit test — a live Jimmolate demo. Binds a JS predicate BEFORE boot
// (the Bootsharp [Import] rule), enables Jimmolate, then runs a passthrough list
// search so the base filter passes everything and the scalar probe does the culling.
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { loadBootResourcesFromDir } from "../../motely-wasm/dist/node-boot.mjs";

const testsDir = dirname(fileURLToPath(import.meta.url));
const entryPath = resolve(testsDir, "..", "..", "motely-wasm", "dist", "index.mjs");
const binDir = resolve(dirname(entryPath), "..", "bin");

const { default: bootsharp, Motely } = await import(pathToFileURL(entryPath).href);
Motely.reportWasmError = (m) => console.error("[WASM ERROR]", m);

const visited = [];
// MY predicate: keep seeds that start with "PI". hand-written scalar logic,
// exactly the Immolate-style "just code against the seed" experience.
Motely.jimmolateProbe = (seed, _deck, _stake) => {
    visited.push(seed);
    return seed.startsWith("PI");
};

await bootsharp.boot(await loadBootResourcesFromDir(binDir));
Motely.enableJimmolate();

const seeds = ["PIFREAK1", "PIAAAAAA", "XYZABCDE", "PILOVESU", "MMMMMMMM"];
const matches = [];
const onMatch = (s) => matches.push(s);
Motely.onSeedMatch.subscribe(onMatch);
let r;
try {
    r = Motely.runPassthroughListSearch(seeds);
} finally {
    Motely.onSeedMatch.unsubscribe(onMatch);
}

console.log("=== JIMMOLATE DEMO ===");
console.log("  predicate : seed => seed.startsWith('PI')");
console.log("  input     :", seeds.join(", "));
console.log("  visited   :", visited.sort().join(", "), `(${visited.length})`);
console.log("  MATCHED   :", matches.sort().join(", "), `(${matches.length})`);
console.log("  engine    : totalSearched =", Number(r.totalSeedsSearched), " matchingSeeds =", Number(r.matchingSeeds));
