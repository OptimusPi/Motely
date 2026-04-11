/**
 * Finds a seed where Gros Michel appears in antes [1,2,3] using ONLY motely-wasm-compat
 * (rebuild: dotnet publish Motely.BrowserWasm -c Release /p:MotelyVersion=…).
 * Uses startRandomSearchFromJaml so JAML is parsed inside WASM (JamlConfig round-trip from loadJaml breaks random search).
 */
import dotnet, { MotelyWasmHost, SearchEvents } from "../Motely.BrowserWasm/motely-wasm-compat/index.mjs";

const jaml = `
name: Gros Michel (WASM-only)
deck: Red
stake: White
must:
  - joker: GrosMichel
    antes: [1, 2, 3]
`;

const RANDOM_PER_BATCH = 500_000;
const MAX_BATCHES = 50;

await dotnet.boot();

for (let batch = 0; batch < MAX_BATCHES; batch++) {
  let first = null;

  function onResult(seed, score, tally) {
    if (first == null) first = { seed, score, tally };
  }

  await new Promise((resolve, reject) => {
    const t = setTimeout(() => reject(new Error("batch timeout (5 min)")), 300_000);
    function onComplete() {
      clearTimeout(t);
      SearchEvents.onComplete.unsubscribe(onComplete);
      SearchEvents.onResult.unsubscribe(onResult);
      resolve();
    }
    SearchEvents.onComplete.subscribe(onComplete);
    SearchEvents.onResult.subscribe(onResult);
    MotelyWasmHost.startRandomSearchFromJaml(jaml, RANDOM_PER_BATCH);
  });

  if (first) {
    console.log(
      JSON.stringify(
        {
          ok: true,
          seed: first.seed,
          score: first.score,
          batch: batch + 1,
          randomSeedsThisBatch: RANDOM_PER_BATCH,
        },
        null,
        2
      )
    );
    process.exit(0);
  }
  console.error(
    `WASM batch ${batch + 1}: no match in ${RANDOM_PER_BATCH} random draws; continuing…`
  );
}

console.error(JSON.stringify({ ok: false, message: "no match within batch cap" }));
process.exit(1);
