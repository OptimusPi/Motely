import dotnet, {
  Motely,
  MotelySingleSearchContext,
  MotelyWasmHost,
  SearchEvents,
} from "../Motely.BrowserWasm/motely-wasm-compat/index.mjs";

const jaml = JSON.stringify({
  deck: "Red",
  stake: "White",
  must: [{ joker: "Blueprint" }],
});

async function main() {
  console.log("Bootsharp docs contract: embedded package => await boot()");
  await dotnet.boot();
  console.log("boot ok");
  console.log("version:", MotelyWasmHost.getVersion());

  const config = MotelyWasmHost.loadJaml(jaml);
  console.log("loadJaml ok:", {
    deck: config.deck,
    stake: config.stake,
  });

  const hostCtx = MotelyWasmHost.motelySingleSearchContext(
    "ALEEB5N",
    Motely.MotelyDeck.Red,
    Motely.MotelyStake.White
  );
  const directCtx = MotelySingleSearchContext.open(
    "ALEEB5N",
    Motely.MotelyDeck.Red,
    Motely.MotelyStake.White
  );

  for (const [label, ctx] of [
    ["hostCtx", hostCtx],
    ["directCtx", directCtx],
  ]) {
    console.log(label, "own keys:", Object.keys(ctx).sort());
    console.log(
      label,
      "proto keys:",
      Object.getOwnPropertyNames(Object.getPrototypeOf(ctx)).sort()
    );
    console.log(label, "typeof getBossForAnte:", typeof ctx.getBossForAnte);
    console.log(label, "typeof getAnteFirstVoucher:", typeof ctx.getAnteFirstVoucher);
    console.log(label, "typeof getNextShopItem:", typeof ctx.getNextShopItem);
  }

  // Stable contract proof: single-seed queries through host methods with primitive args.
  const seed = "ALEEB5N";
  const deck = Motely.MotelyDeck.Red;
  const stake = Motely.MotelyStake.White;
  console.log("single host calls:", {
    boss1: String(MotelyWasmHost.singleGetBossForAnte(seed, deck, stake, 1)),
    voucher1: String(MotelyWasmHost.singleGetAnteFirstVoucher(seed, deck, stake, 1)),
    tag1: String(MotelyWasmHost.singleGetNextTag(seed, deck, stake, 1)),
    lucky1: MotelyWasmHost.singleGetNextLuckyMoney(seed, deck, stake, 1),
    misprint1: MotelyWasmHost.singleGetNextMisprintMult(seed, deck, stake),
  });

  const results = [];
  const onResult = (seed, score, tally) => {
    results.push({ seed, score, tally: Array.from(tally) });
    if (results.length <= 3) {
      console.log("result", results.length, { seed, score, tally: Array.from(tally) });
    }
  };
  const onProgress = (searched, matching) => {
    if (searched <= 10000n || searched % 50000n === 0n) {
      console.log("progress", searched.toString(), matching.toString());
    }
  };
  const onComplete = (status, searched, matching) => {
    console.log("complete", {
      status,
      searched: searched.toString(),
      matching: matching.toString(),
      resultCount: results.length,
    });
    SearchEvents.onResult.unsubscribe(onResult);
    SearchEvents.onProgress.unsubscribe(onProgress);
    SearchEvents.onComplete.unsubscribe(onComplete);
  };

  SearchEvents.onResult.subscribe(onResult);
  SearchEvents.onProgress.subscribe(onProgress);
  SearchEvents.onComplete.subscribe(onComplete);

  console.log("trying host seed-list search (from jaml)...");
  MotelyWasmHost.startSeedListSearchFromJaml(jaml, ["ALEEB5N"]);
  await new Promise((resolve) => setTimeout(resolve, 300));
}

main().catch((err) => {
  console.error(err);
  process.exitCode = 1;
});
