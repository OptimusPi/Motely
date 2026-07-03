// A real finder, authored in JavaScript, run inside the Motely kernel — the OG Immolate
// contract: filter(inst) => keep?. The filter identifies ALEEB among decoys by its VERIFIED
// ante-1 fingerprint — Magic Trick voucher AND The Window boss — two independent derivations
// read live off the genuine MotelySingleSearchContext.
// Run from Motely.Wasm/: node examples/jimmolate-finder.mjs
import bootsharp, {
  MotelyJaml,
  MotelySearch,
  Jimmolate,
  MotelyVoucher,
  MotelyBossBlind,
} from "../dist/index.mjs";

const found = [];

// The OG Immolate contract: filter returns a NUMBER (a score), never a JS boolean —
// the engine keeps every seed whose score reaches the cutoff (default 1).
Jimmolate.filter = (inst) => {
  if (inst.getAnteFirstVoucher(1) !== MotelyVoucher.MagicTrick) return 0;
  const result = inst.getBossForAnteWithState(1, inst.newRunState());
  return result.boss === MotelyBossBlind.TheWindow ? 1 : 0;
};

MotelySearch.onSeedMatch.subscribe((seed) => {
  found.push(seed);
  console.log("FOUND:", seed);
});
// Every exported event stays wired — the bridge invokes them all during a search.
MotelySearch.onProgress.subscribe(() => {});
MotelySearch.onScoredResult.subscribe(() => {});

await bootsharp.boot();

const jaml = MotelyJaml.fromYaml(`name: aleeb-finder
deck: Red
stake: White
seeds: [PIROCKS, ALEEB, LOVEYAHB]
`);

try {
  await MotelySearch.searchList(jaml);
} catch (e) {
  console.error("SEARCH THREW:", e?.message ?? e);
  console.error(e?.stack?.split("\n").slice(0, 3).join("\n"));
  process.exit(1);
}

if (found.length === 1 && found[0] === "ALEEB") {
  console.log("the JS filter pulled the needle out of the decoys.");
} else {
  console.error(`expected exactly [ALEEB], got [${found}]`);
  process.exit(1);
}
