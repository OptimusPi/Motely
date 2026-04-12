import test from "node:test";
import assert from "node:assert/strict";

import dotnet, {
  Motely,
  MotelyWasmHost,
  MotelySingleSearchContext,
  SearchEvents,
} from "../Motely.BrowserWasm/motely-wasm/index.mjs";

const jaml = JSON.stringify({
  deck: "Red",
  stake: "White",
  must: [{ joker: "Blueprint" }],
});

let bootPromise = null;

async function ensureBooted() {
  bootPromise ??= dotnet.boot().catch((error) => {
    bootPromise = null;
    throw error;
  });
  await bootPromise;
}

test("boot + loadJaml contract", async () => {
  await ensureBooted();
  const version = MotelyWasmHost.getVersion();
  assert.equal(typeof version, "string");
  assert.ok(version.length > 0, "version should be non-empty");

  const config = MotelyWasmHost.loadJaml(jaml);
  assert.equal(config.deck, Motely.MotelyDeck.Red);
  assert.equal(config.stake, Motely.MotelyStake.White);
});

test("single-search export shape is available", async () => {
  await ensureBooted();
  const ctx = MotelySingleSearchContext.open(
    "ALEEB5N",
    Motely.MotelyDeck.Red,
    Motely.MotelyStake.White
  );

  assert.equal(typeof ctx.getSeed, "function");
  assert.equal(typeof ctx.getBossForAnte, "function");
  assert.equal(typeof ctx.getAnteFirstVoucher, "function");
  assert.equal(typeof ctx.getNextTag, "function");
  assert.equal(ctx.getSeed(), "ALEEB5N");
});

test("host single-query methods accept parameters", async () => {
  await ensureBooted();
  const seed = "ALEEB5N";
  const deck = Motely.MotelyDeck.Red;
  const stake = Motely.MotelyStake.White;

  const boss = MotelyWasmHost.singleGetBossForAnte(seed, deck, stake, 1);
  const voucher = MotelyWasmHost.singleGetAnteFirstVoucher(seed, deck, stake, 1);
  const tag = MotelyWasmHost.singleGetNextTag(seed, deck, stake, 1);
  const lucky = MotelyWasmHost.singleGetNextLuckyMoney(seed, deck, stake, 1);
  const misprint = MotelyWasmHost.singleGetNextMisprintMult(seed, deck, stake);

  assert.equal(typeof boss, "number");
  assert.equal(typeof voucher, "number");
  assert.equal(typeof tag, "number");
  assert.equal(typeof lucky, "boolean");
  assert.equal(typeof misprint, "number");
});

test("search starts from jaml string and event contract remains stable", async () => {
  await ensureBooted();

  const seen = [];
  let timeout = null;
  let onComplete = null;
  let completed = false;
  const onResult = (seed, score, tally) => {
    seen.push({ seed, score, tally });
  };
  const complete = new Promise((resolve, reject) => {
    timeout = setTimeout(() => {
      reject(new Error("Timed out waiting for search completion"));
    }, 15000);
    onComplete = () => {
      completed = true;
      resolve();
    };
    SearchEvents.onComplete.subscribe(onComplete);
  });

  SearchEvents.onResult.subscribe(onResult);
  try {
    MotelyWasmHost.startSeedListSearchFromJaml(jaml, ["ALEEB5N"]);
    await complete;
  } finally {
    if (timeout) {
      clearTimeout(timeout);
    }
    if (onComplete) {
      SearchEvents.onComplete.unsubscribe(onComplete);
    }
    SearchEvents.onResult.unsubscribe(onResult);
  }

  assert.equal(completed, true);
  if (seen.length > 0) {
    const first = seen[0];
    assert.equal(typeof first.seed, "string");
    assert.equal(typeof first.score, "number");
    assert.ok(first.tally instanceof Int32Array, "tally should be Int32Array");
  }
});
