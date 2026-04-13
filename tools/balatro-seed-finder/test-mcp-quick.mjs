import dotnet, { MotelyWasmHost, SearchEvents } from "motely-wasm";

await dotnet.boot();
console.log("version:", MotelyWasmHost.getVersion());

const jaml = JSON.stringify({ deck: "Red", stake: "White", must: [{ joker: "Blueprint" }] });

// Use startRandomSearchFromJaml (raw JAML, no configId)
const results = [];
const complete = new Promise((resolve, reject) => {
  const timeout = setTimeout(() => reject(new Error("Timed out")), 15000);
  const onComplete = (status, searched, matches) => {
    clearTimeout(timeout);
    SearchEvents.onComplete.unsubscribe(onComplete);
    resolve({ status, searched: searched.toString(), matches: matches.toString() });
  };
  SearchEvents.onComplete.subscribe(onComplete);
});
const onResult = (seed, score, tally) => results.push({ seed, score });
SearchEvents.onResult.subscribe(onResult);

console.log("starting search...");
MotelyWasmHost.startRandomSearchFromJaml(jaml, 5000);
const searchResult = await complete;
SearchEvents.onResult.unsubscribe(onResult);

console.log("search:", searchResult.status, "|", searchResult.matches, "matches from", searchResult.searched);
if (results.length > 0) console.log("top:", results[0].seed, "score:", results[0].score);

// Analyze
const analysis = JSON.parse(MotelyWasmHost.analyzeSeed("ALEEB5N", 0, 0));
console.log("analyze:", analysis.antes.length, "antes, boss1:", analysis.antes[0].boss);

console.log("=== ALL OK ===");
