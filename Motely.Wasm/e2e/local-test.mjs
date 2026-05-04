
// Local motely-wasm integration tests — runs against the fresh dotnet publish output.
// Covers: boot, JAML validation, schema, search context, shop items, jokers, bosses.
// Run: node local-test.mjs  (from this dir, after dotnet publish Motely.Wasm -c Release)

import bootsharp, { Motely } from "../../motely-wasm/index.mjs";

const { MotelyWasm, MotelyWasmEvents } = Motely;

let failures = 0;
let total = 0;
const expect = (name, ok, detail) => {
  total++;
  if (ok) console.log(`  ok   ${name}`);
  else {
    console.log(`  FAIL ${name}${detail ? ` — ${detail}` : ""}`);
    failures++;
  }
};

console.log("Booting motely-wasm (local build)...");
const t0 = Date.now();
await bootsharp.boot();
console.log(`Booted in ${Date.now() - t0}ms\n`);

// ── Version ──
console.log("1. Version");
const version = MotelyWasm.getVersion();
expect("getVersion returns a string", typeof version === "string" && version.length > 0, version);

// ── JAML Validation ──
console.log("\n2. JAML Validation");
const goodJaml = `
name: test
deck: Red
stake: White
must:
  - joker: Blueprint
    antes: [1]
should:
  - uncommonJoker: Any
    score: 10
`;
const v1 = MotelyWasm.validateJamlStructured(goodJaml);
expect("valid JAML passes", v1.valid === true, JSON.stringify(v1));

const badJaml = `must:\n  - joker: NotARealJokerName`;
const v2 = MotelyWasm.validateJamlStructured(badJaml);
expect("invalid joker name rejected", v2.valid === false);

const typoJaml = `must:\n  - boses: TheArm`;
const v3 = MotelyWasm.validateJamlStructured(typoJaml);
expect("typo'd key 'boses' rejected by strict mode", v3.valid === false);

// ── standardCard with rank ──
console.log("\n3. standardCard clause");
const cardJaml = `
name: king-test
deck: Red
stake: White
must:
  - standardCard:
      rank: K
`;
const vc = MotelyWasm.validateJamlStructured(cardJaml);
expect("standardCard with rank: K is valid", vc.valid === true, JSON.stringify(vc));

// ── Schema ──
console.log("\n4. JAML Schema");
const schemaJson = MotelyWasm.getJamlSchema();
const schema = JSON.parse(schemaJson);
expect("schema parses as JSON", typeof schema === "object");
expect("schema has $schema", typeof schema.$schema === "string");
expect("schema has $defs.Joker", Array.isArray(schema?.$defs?.Joker?.enum));
expect("Joker enum includes Blueprint", schema.$defs.Joker.enum.includes("Blueprint"));
expect("schema has $defs.Boss", Array.isArray(schema?.$defs?.Boss?.enum));

// ── Search Context — seed analysis ──
console.log("\n5. Search Context (seed: 1AAAAAAA, Red, White)");
const ctx = MotelyWasm.createSearchContext("1AAAAAAA", Motely.MotelyDeck.Red, Motely.MotelyStake.White);
expect("createSearchContext returns an object", ctx != null);

// Boss stream
console.log("\n6. Boss stream");
const bossStream = ctx.createBossStream();
expect("createBossStream returns", bossStream != null);
const runState = { prngState: 0 };
const boss1 = ctx.getNextBossForAnte(bossStream, 1, runState);
expect("ante 1 boss is a number (enum)", typeof boss1.boss === "number");
expect("boss value > 0", boss1.boss > 0, `got ${boss1.boss}`);

// Boss chunk for antes 1-8
const bossChunk = ctx.getNextBossForAnteChunk(bossStream, 1, 8, runState);
expect("boss chunk has 8 entries", bossChunk.bosses.length === 8, `got ${bossChunk.bosses.length}`);

// ── Shop items ──
console.log("\n7. Shop items");
const shopStream = ctx.createShopItemStream(1, runState, 0, 0);
expect("createShopItemStream returns", shopStream != null);

const shopItem = ctx.getNextShopItem(shopStream);
expect("getNextShopItem returns an item", shopItem.item != null);
const itemValue = typeof shopItem.item === "number" ? shopItem.item : shopItem.item?.value;
expect("shop item has a numeric value", typeof itemValue === "number", `got ${typeof itemValue}: ${JSON.stringify(shopItem.item)}`);
if (typeof itemValue === "number") {
  expect("shop item value is nonzero (valid packed item)", itemValue !== 0, `value was 0`);
}

// Shop item chunk
const shopChunk = ctx.getNextShopItemChunk(shopStream, 5);
// Bootsharp marshals C# int[] → JS Int32Array (NOT Array). Consumers must handle typed arrays.
const isTypedOrArray = shopChunk.items instanceof Int32Array || Array.isArray(shopChunk.items);
expect("shop item chunk returns Int32Array or Array", isTypedOrArray, `got ${shopChunk.items?.constructor?.name}`);
expect("chunk has items", shopChunk.items.length > 0, `got ${shopChunk.items.length}`);
if (shopChunk.items.length > 0) {
  expect("chunk items are numbers (packed ints)", typeof shopChunk.items[0] === "number");
}

// ── Joker stream ──
console.log("\n8. Shop jokers");
const jokerStream = ctx.createShopJokerStream(1, 0);
const joker1 = ctx.getNextShopJoker(jokerStream);
expect("shop joker returns", joker1.item != null);
const jokerValue = typeof joker1.item === "number" ? joker1.item : joker1.item?.value;
expect("joker has numeric value", typeof jokerValue === "number", `${typeof jokerValue}: ${JSON.stringify(joker1.item)}`);

// ── Search ──
console.log("\n9. Random search (100 seeds)");
const results = [];
MotelyWasmEvents.notifyResult = (seed, score, tallies) => {
  results.push({ seed, score });
};
let progressCount = 0;
MotelyWasmEvents.notifyProgress = () => progressCount++;

const search = MotelyWasm.startRandomSearch(goodJaml, 100);
expect("startRandomSearch returns", search != null);
const snap = search.getSnapshot();
expect("snapshot has totalSeedsSearched", typeof snap.totalSeedsSearched === "bigint" || typeof snap.totalSeedsSearched === "number");
expect("searched >= 100", Number(snap.totalSeedsSearched) >= 100, `searched ${snap.totalSeedsSearched}`);
expect("got some results or zero matches (both valid)", results.length >= 0);
console.log(`  info: ${results.length} matches out of ${snap.totalSeedsSearched} seeds`);

// Cleanup
ctx.dispose?.() ?? ctx[Symbol.dispose]?.();

// ── Summary ──
console.log(`\n${"=".repeat(50)}`);
if (failures === 0) {
  console.log(`PASS — ${total} assertions, 0 failures.`);
  process.exit(0);
} else {
  console.log(`FAIL — ${total} assertions, ${failures} failure(s).`);
  process.exit(1);
}
