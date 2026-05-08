import bootsharp, { Motely } from "../../motely-wasm/index.mjs";

const { MotelyWasm } = Motely;

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

console.log("Booting motely-wasm...");
await bootsharp.boot();

const createCtx = () =>
  MotelyWasm.createSearchContext(
    "1AAAAAAA",
    Motely.MotelyDeck.Red,
    Motely.MotelyStake.White,
  );

console.log("Manual LuckyMoney stream usage...");

const ctxSingle = createCtx();
const singleStream0 = ctxSingle.createLuckyCardMoneyStream();
const first = ctxSingle.getNextLuckyMoney(singleStream0, 1);
expect("single LuckyMoney result is boolean", typeof first.value === "boolean", typeof first.value);

const second = ctxSingle.getNextLuckyMoney(first.stream, 1);
expect("second LuckyMoney result is boolean", typeof second.value === "boolean", typeof second.value);

const ctxChunk = createCtx();
const chunkStream0 = ctxChunk.createLuckyCardMoneyStream();
const chunk = ctxChunk.getNextLuckyMoneyChunk(chunkStream0, 8, 1);

expect(
  "LuckyMoney chunk returns bool array or typed array",
  Array.isArray(chunk.values) || chunk.values instanceof Uint8Array || chunk.values instanceof Int8Array,
  chunk.values?.constructor?.name,
);
expect("LuckyMoney chunk length is 8", chunk.values.length === 8, `got ${chunk.values.length}`);
expect(
  "LuckyMoney chunk entries are booleans or 0/1 values",
  Array.from(chunk.values).every((value) => typeof value === "boolean" || value === 0 || value === 1),
  JSON.stringify(Array.from(chunk.values)),
);

const ctxCompare = createCtx();
let compareStream = ctxCompare.createLuckyCardMoneyStream();
const manual = [];
for (let i = 0; i < 8; i++) {
  const next = ctxCompare.getNextLuckyMoney(compareStream, 1);
  manual.push(next.value);
  compareStream = next.stream;
}

expect(
  "chunk LuckyMoney matches repeated single-step LuckyMoney",
  JSON.stringify(Array.from(chunk.values)) === JSON.stringify(manual),
  `chunk=${JSON.stringify(Array.from(chunk.values))} manual=${JSON.stringify(manual)}`,
);

ctxSingle.dispose?.() ?? ctxSingle[Symbol.dispose]?.();
ctxChunk.dispose?.() ?? ctxChunk[Symbol.dispose]?.();
ctxCompare.dispose?.() ?? ctxCompare[Symbol.dispose]?.();

console.log("");
if (failures === 0) {
  console.log(`PASS -- ${total} assertions, 0 failures.`);
  process.exit(0);
} else {
  console.log(`FAIL -- ${total} assertions, ${failures} failure(s).`);
  process.exit(1);
}