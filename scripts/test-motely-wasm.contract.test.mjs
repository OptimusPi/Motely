import test from "node:test";
import assert from "node:assert/strict";

import dotnet, {
  Filters,
  Motely,
  MotelyWasmHost,
  SearchEvents,
} from "motely-wasm";

const jaml = JSON.stringify({
  deck: "Red",
  stake: "White",
  must: [{ joker: "Blueprint" }],
});

const SEED = "ALEEB5N";
const DECK = Motely.MotelyDeck.Red;
const STAKE = Motely.MotelyStake.White;

let bootPromise = null;

async function ensureBooted() {
  bootPromise ??= dotnet.boot().catch((error) => {
    bootPromise = null;
    throw error;
  });
  await bootPromise;
}

async function runSearch(startFn, timeoutMs = 15000) {
  const seen = [];
  const progress = [];
  let completion = null;
  const done = new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error("Search timed out")), timeoutMs);
    const onComplete = (status, searched, matching) => {
      clearTimeout(timeout);
      completion = { status, searched, matching };
      SearchEvents.onComplete.unsubscribe(onComplete);
      resolve();
    };
    SearchEvents.onComplete.subscribe(onComplete);
  });
  const onResult = (seed, score, tally) => seen.push({ seed, score, tally });
  const onProgress = (searched, matching) => progress.push({ searched, matching });
  SearchEvents.onResult.subscribe(onResult);
  SearchEvents.onProgress.subscribe(onProgress);
  try {
    startFn();
    await done;
    return { seen, progress, completion };
  } finally {
    SearchEvents.onResult.unsubscribe(onResult);
    SearchEvents.onProgress.unsubscribe(onProgress);
  }
}

test("boot + getVersion", async () => {
  await ensureBooted();
  const version = MotelyWasmHost.getVersion();
  assert.equal(typeof version, "string");
  assert.ok(version.length > 0, "version should be non-empty");
  console.log("  version:", version);
});

test("loadJaml returns config ID + getConfigDeck/Stake", async () => {
  await ensureBooted();
  const configId = MotelyWasmHost.loadJaml(jaml);
  assert.equal(typeof configId, "string");
  assert.ok(configId.length > 0);
  assert.equal(MotelyWasmHost.getConfigDeck(configId), DECK);
  assert.equal(MotelyWasmHost.getConfigStake(configId), STAKE);
});

test("validateJaml returns valid or error string", async () => {
  await ensureBooted();
  assert.equal(MotelyWasmHost.validateJaml(jaml), "valid");
  const bad = MotelyWasmHost.validateJaml("not json at all");
  assert.notEqual(bad, "valid");
  assert.equal(typeof bad, "string");
});

test("openSingleSearchContext + all context methods", async () => {
  await ensureBooted();
  const ctxId = MotelyWasmHost.openSingleSearchContext(SEED, DECK, STAKE);
  assert.equal(typeof ctxId, "string");

  assert.equal(MotelyWasmHost.contextGetSeed(ctxId), SEED);

  const boss = MotelyWasmHost.contextGetBossForAnte(ctxId, 1);
  assert.equal(typeof boss, "number");

  const voucher = MotelyWasmHost.contextGetAnteFirstVoucher(ctxId, 1);
  assert.equal(typeof voucher, "number");

  const tag = MotelyWasmHost.contextGetNextTag(ctxId, 1);
  assert.equal(typeof tag, "number");

  const shopItem = MotelyWasmHost.contextGetNextShopItem(ctxId, 1);
  assert.equal(typeof shopItem, "object");
  assert.ok(shopItem !== null);
  assert.equal(typeof shopItem.value, "number", "MotelyItem.value should be a packed int");

  const luckyMoney = MotelyWasmHost.contextGetNextLuckyMoney(ctxId, 4.0);
  assert.equal(typeof luckyMoney, "boolean");

  const luckyMult = MotelyWasmHost.contextGetNextLuckyMult(ctxId, 4.0);
  assert.equal(typeof luckyMult, "boolean");

  const misprintMult = MotelyWasmHost.contextGetNextMisprintMult(ctxId);
  assert.equal(typeof misprintMult, "number");

  MotelyWasmHost.contextClose(ctxId);
});

test("analyzeSeed returns valid JSON with antes", async () => {
  await ensureBooted();
  const json = MotelyWasmHost.analyzeSeed(SEED, DECK, STAKE);
  assert.equal(typeof json, "string");
  const parsed = JSON.parse(json);
  assert.ok(Array.isArray(parsed.antes));
  assert.ok(parsed.antes.length > 0);
  console.log("  antes:", parsed.antes.length, "| boss1:", parsed.antes[0].boss);
});

test("startRandomSearch + onProgress types + onResult types", async () => {
  await ensureBooted();
  const configId = MotelyWasmHost.loadJaml(jaml);
  const { seen, progress } = await runSearch(() =>
    MotelyWasmHost.startRandomSearch(configId, 10000)
  );
  console.log("  results:", seen.length, "from 10k seeds");
  if (seen.length > 0) {
    const { seed, score, tally } = seen[0];
    assert.equal(typeof seed, "string");
    assert.equal(typeof score, "number");
    assert.ok(tally instanceof Int32Array, "tally should be Int32Array");
  }
  assert.ok(progress.length > 0, "onProgress should fire at least once");
  const { searched, matching } = progress[0];
  assert.equal(typeof searched, "bigint", "searched should be BigInt");
  assert.equal(typeof matching, "bigint", "matching should be BigInt");
});

test("startRandomSearchFromJaml", async () => {
  await ensureBooted();
  const { seen } = await runSearch(() =>
    MotelyWasmHost.startRandomSearchFromJaml(jaml, 5000)
  );
  console.log("  results:", seen.length, "from 5k seeds");
});

test("startSeedListSearch (configId variant)", async () => {
  await ensureBooted();
  const configId = MotelyWasmHost.loadJaml(jaml);
  const { completion } = await runSearch(() =>
    MotelyWasmHost.startSeedListSearch(configId, [SEED])
  );
  assert.ok(completion !== null);
});

test("startSeedListSearchFromJaml", async () => {
  await ensureBooted();
  const { completion } = await runSearch(() =>
    MotelyWasmHost.startSeedListSearchFromJaml(jaml, [SEED])
  );
  assert.ok(completion !== null);
});

test("startKeywordSearch", async () => {
  await ensureBooted();
  const configId = MotelyWasmHost.loadJaml(
    JSON.stringify({ deck: "Red", stake: "White" })
  );
  const { seen, completion } = await runSearch(() =>
    MotelyWasmHost.startKeywordSearch(configId, "ACE", "")
  );
  console.log("  keyword 'ACE' results:", seen.length);
  assert.ok(completion !== null);
});

test("startAestheticSearch (Palindrome)", async () => {
  await ensureBooted();
  const configId = MotelyWasmHost.loadJaml(
    JSON.stringify({ deck: "Red", stake: "White" })
  );
  const { seen, completion } = await runSearch(
    () => MotelyWasmHost.startAestheticSearch(configId, Filters.JamlAesthetic.Palindrome),
    60000
  );
  console.log("  palindrome results:", seen.length);
  assert.ok(completion !== null);
  if (seen.length > 0) {
    assert.equal(typeof seen[0].seed, "string");
  }
});

test("startConfiguredSearch (BigInt batch params)", async () => {
  await ensureBooted();
  const configId = MotelyWasmHost.loadJaml(jaml);
  const { completion } = await runSearch(() =>
    MotelyWasmHost.startConfiguredSearch(configId, 4, 0n, 0n)
  );
  assert.ok(completion !== null);
});

test("startSequentialSearch (BigInt batch params)", async () => {
  await ensureBooted();
  const configId = MotelyWasmHost.loadJaml(jaml);
  const { completion } = await runSearch(() =>
    MotelyWasmHost.startSequentialSearch(configId, 4, 0n, 0n)
  );
  assert.ok(completion !== null);
});

test("stopSearch does not throw when idle", async () => {
  await ensureBooted();
  assert.doesNotThrow(() => MotelyWasmHost.stopSearch());
});
