// Temporary v20 smoke test — boots the EMBEDDED build and runs a real bounded
// search end to end. Delete after use. Run: node .smoke-test.mjs
import bootsharp from "./dist/index.mjs";
import { Program } from "./dist/generated/modules/motely/wasm.g.mjs";

const t0 = Date.now();
const log = (m) => console.log(`[+${((Date.now() - t0) / 1000).toFixed(2)}s] ${m}`);

let progressTicks = 0;
let scored = 0;
let firstMatch = null;

Program.onProgress.subscribe(() => progressTicks++);
Program.onSeedMatch.subscribe((seed) => {
  if (!firstMatch) firstMatch = seed;
});
Program.onScoredResult.subscribe((r) => {
  scored++;
  if (scored <= 3) log(`  scored seed=${r.seed} score=${r.score} tallies=[${r.tallies}]`);
});

log("booting embedded WASM (no args)...");
await bootsharp.boot();
log(`booted. status=${bootsharp.getStatus()} (expect 2=Booted)`);

// 1. JAML parse — throws on invalid.
const JAML = `must:
  - joker: Blueprint
    antes: [1, 2, 3, 4, 5, 6, 7, 8]
deck: Red
stake: White
`;
const config = Program.parseJaml(JAML);
log(`parseJaml OK: must=${config.must?.length ?? "?"} deck=${config.deck} stake=${config.stake}`);

// 2. JAML <-> JSON roundtrip.
const json = Program.jamlToJson(JAML);
const backToJaml = Program.jsonToJaml(json);
log(`jamlToJson OK (${json.length} chars), jsonToJaml OK (${backToJaml.length} chars)`);

// 3. Single-seed analysis.
const snap = Program.jamlyzer("ALEEB", config);
log(`jamlyzer("ALEEB") OK: ${snap ? "snapshot returned" : "NULL"}`);

// 4. A real bounded random search (small N — NOT a full sweep).
log("running runRandomSearch(config, 25000)...");
const search = Program.runRandomSearch(config, 25000);
log(
  `search done: searched=${search.totalSeedsSearched} matches=${search.matchingSeeds} ` +
    `completed=${search.isCompleted}`
);
log(`events fired: progress=${progressTicks} scored=${scored} firstMatch=${firstMatch ?? "(none)"}`);

// Verdict.
const ok =
  bootsharp.getStatus() === 2 &&
  config.deck === "Red" &&
  json.length > 0 &&
  search.isCompleted &&
  search.totalSeedsSearched > 0n;
log(ok ? "✅ SMOKE TEST PASSED" : "❌ SMOKE TEST FAILED");
process.exit(ok ? 0 : 1);
