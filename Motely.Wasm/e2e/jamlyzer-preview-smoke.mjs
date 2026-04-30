// Local Jamlyzer preview smoke test.
// Run from Motely.Wasm/e2e after:
//   dotnet publish ../../Motely.Wasm/Motely.Wasm.csproj -c Release
//
// Verifies that the WASM export can analyze a seed and mark JAML-matched
// inspected preview items with `matched: true`.

import bootsharp, { MotelyWasm } from "../../motely-wasm/index.mjs";

let failures = 0;
let total = 0;

const expect = (name, ok, detail) => {
  total++;
  if (ok) {
    console.log(`  ok   ${name}`);
  } else {
    console.log(`  FAIL ${name}${detail ? ` -- ${detail}` : ""}`);
    failures++;
  }
};

console.log("Booting motely-wasm local build...");
const t0 = Date.now();
await bootsharp.boot();
console.log(`Booted in ${Date.now() - t0}ms`);
console.log("");

const jaml = `
name: aleeb-jamlyzer-preview
deck: Red
stake: White
must:
  - joker: Any
    antes: [1]
`;

console.log("Analyzing ALEEB with JAML-driven preview highlights...");
const result = MotelyWasm.analyzeJamlSeeds(jaml, ["ALEEB"]);
const seed = result.seeds?.[0];
const anteOne = seed?.analysis?.antes?.find((ante) => ante.ante === 1);
const shopMatches = anteOne?.shopQueue?.filter((item) => item.matched) ?? [];
const packMatches = anteOne?.packs?.flatMap((pack) => pack.items ?? []).filter((item) => item.matched) ?? [];
const matchedCount = shopMatches.length + packMatches.length;

expect("analyzeJamlSeeds returns no error", !result.error, result.error);
expect("one matching seed returned", result.seeds?.length === 1, `got ${result.seeds?.length}`);
expect("seed is ALEEB", seed?.seed === "ALEEB", seed?.seed);
expect("analysis includes ante 1", anteOne != null);
expect("matched preview items exist", matchedCount > 0, `matched=${matchedCount}`);
expect("matched items carry packed value", [...shopMatches, ...packMatches].every((item) => typeof item.value === "number"));

console.log("");
console.log(`Matched preview items: ${matchedCount}`);
console.log(`Runtime version: ${MotelyWasm.getVersion()}`);
console.log("");

if (failures === 0) {
  console.log(`PASS -- ${total} assertions, 0 failures.`);
  process.exit(0);
} else {
  console.log(`FAIL -- ${total} assertions, ${failures} failure(s).`);
  process.exit(1);
}
