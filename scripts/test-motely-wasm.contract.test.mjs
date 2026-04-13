import test from "node:test";
import assert from "node:assert/strict";

import dotnet, {
  Motely,
  MotelyWasmHost,
  SearchEvents,
} from "motely-wasm";

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

test("boot + getVersion", async () => {
  await ensureBooted();
  const version = MotelyWasmHost.getVersion();
  assert.equal(typeof version, "string");
  assert.ok(version.length > 0, "version should be non-empty");
  console.log("  version:", version);
});

test("loadJaml returns config ID string", async () => {
  await ensureBooted();
  const configId = MotelyWasmHost.loadJaml(jaml);
  assert.equal(typeof configId, "string");
  assert.ok(configId.length > 0, "config ID should be non-empty");

  const deck = MotelyWasmHost.getConfigDeck(configId);
  const stake = MotelyWasmHost.getConfigStake(configId);
  assert.equal(deck, Motely.MotelyDeck.Red);
  assert.equal(stake, Motely.MotelyStake.White);
});

test("validateJaml returns valid or error", async () => {
  await ensureBooted();
  const good = MotelyWasmHost.validateJaml(jaml);
  assert.equal(good, "valid");

  const bad = MotelyWasmHost.validateJaml("not json at all");
  assert.notEqual(bad, "valid");
  assert.equal(typeof bad, "string");
});

test("openSingleSearchContext + context methods", async () => {
  await ensureBooted();
  const ctxId = MotelyWasmHost.openSingleSearchContext(
    "ALEEB5N",
    Motely.MotelyDeck.Red,
    Motely.MotelyStake.White
  );
  assert.equal(typeof ctxId, "string");

  const seed = MotelyWasmHost.contextGetSeed(ctxId);
  assert.equal(seed, "ALEEB5N");

  const boss = MotelyWasmHost.contextGetBossForAnte(ctxId, 1);
  assert.equal(typeof boss, "number");

  const voucher = MotelyWasmHost.contextGetAnteFirstVoucher(ctxId, 1);
  assert.equal(typeof voucher, "number");

  const tag = MotelyWasmHost.contextGetNextTag(ctxId, 1);
  assert.equal(typeof tag, "number");

  MotelyWasmHost.contextClose(ctxId);
});

test("analyzeSeed returns JSON string", async () => {
  await ensureBooted();
  const json = MotelyWasmHost.analyzeSeed(
    "ALEEB5N",
    Motely.MotelyDeck.Red,
    Motely.MotelyStake.White
  );
  assert.equal(typeof json, "string");
  const parsed = JSON.parse(json);
  assert.ok(parsed.antes, "should have antes array");
  assert.ok(parsed.antes.length > 0, "should have at least one ante");
  console.log("  antes:", parsed.antes.length, "| boss1:", parsed.antes[0].boss);
});

test("startRandomSearch + events", async () => {
  await ensureBooted();
  const configId = MotelyWasmHost.loadJaml(jaml);

  const seen = [];
  let completed = false;
  const complete = new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error("Timed out")), 15000);
    const onComplete = () => {
      completed = true;
      clearTimeout(timeout);
      SearchEvents.onComplete.unsubscribe(onComplete);
      resolve();
    };
    SearchEvents.onComplete.subscribe(onComplete);
  });

  const onResult = (seed, score, tally) => {
    seen.push({ seed, score, tally });
  };
  SearchEvents.onResult.subscribe(onResult);

  try {
    MotelyWasmHost.startRandomSearch(configId, 10000);
    await complete;
  } finally {
    SearchEvents.onResult.unsubscribe(onResult);
  }

  assert.equal(completed, true);
  console.log("  results:", seen.length, "from 10k seeds");
  if (seen.length > 0) {
    const first = seen[0];
    assert.equal(typeof first.seed, "string");
    assert.equal(typeof first.score, "number");
    assert.ok(first.tally instanceof Int32Array, "tally should be Int32Array");
  }
});

test("startSeedListSearchFromJaml (direct jaml string)", async () => {
  await ensureBooted();

  const seen = [];
  let completed = false;
  const complete = new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error("Timed out")), 15000);
    const onComplete = () => {
      completed = true;
      clearTimeout(timeout);
      SearchEvents.onComplete.unsubscribe(onComplete);
      resolve();
    };
    SearchEvents.onComplete.subscribe(onComplete);
  });

  const onResult = (seed, score, tally) => {
    seen.push({ seed, score, tally });
  };
  SearchEvents.onResult.subscribe(onResult);

  try {
    MotelyWasmHost.startSeedListSearchFromJaml(jaml, ["ALEEB5N"]);
    await complete;
  } finally {
    SearchEvents.onResult.unsubscribe(onResult);
  }

  assert.equal(completed, true);
});
