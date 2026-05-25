// The SAME test as C# TestReturnedContext_DrivesShopStreamMatchingAnalyzer,
// but in JS against the WASM build: take the MotelySingleSearchContext that
// getSingleSearchContext() returns and try to drive a shop stream off it.
import { readFile } from "node:fs/promises";
import { fileURLToPath, pathToFileURL } from "node:url";
import path from "node:path";

const pkgDir = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    "../motely-wasm"
);
const toUrl = (rel) => pathToFileURL(path.join(pkgDir, rel)).href;

const bootsharp = (await import(toUrl("dist/index.mjs"))).default;
const { Program } = await import(toUrl("dist/generated/modules/motely/wasm.g.mjs"));
const { MotelyDeck, MotelyStake } = await import(toUrl("dist/generated/modules/motely/enums.g.mjs"));

// Node can't fetch() file:// boot resources, so preload them and hand boot the
// content object (same shape as resources.mjs fetchResources). NativeAOT single
// file: only the wasm has bytes, the rest are empty.
const wasmBytes = await readFile(path.join(pkgDir, "bin/motely-wasm.wasm"));
await bootsharp.boot({
    wasm: wasmBytes.buffer.slice(wasmBytes.byteOffset, wasmBytes.byteOffset + wasmBytes.byteLength),
    assemblies: [],
    icu: [],
    symbols: [],
    pdb: [],
});

// CONTROL: same boot, same path. If these return real data, boot/path are fine
// and any {} below is the struct itself, not a broken setup.
console.log("CONTROL version():", JSON.stringify(Program.version()));
console.log("CONTROL decodeItemType(0):", JSON.stringify(Program.decodeItemType(0)));
console.log("CONTROL nativeFilterNames():", JSON.stringify(Program.nativeFilterNames()));

const ctx = Program.getSingleSearchContext("UNITTEST", MotelyDeck.Red, MotelyStake.White);
console.log("JS context value:", JSON.stringify(ctx));
console.log("JS context keys:", ctx && typeof ctx === "object" ? Object.keys(ctx) : typeof ctx);
console.log("createShopItemStream is a function:", typeof ctx?.createShopItemStream === "function");

let drove = false;
let err = null;
try {
    const shopStream = ctx.createShopItemStream(1, MotelyDeck.Red);
    const items = [];
    for (let i = 0; i < 5; i++) {
        items.push(ctx.getNextShopItem(shopStream).value);
    }
    console.log("shop items off JS context:", items);
    drove = true;
} catch (e) {
    err = String(e);
}

console.log("could drive shop stream off the JS context:", drove);
if (err) console.log("error:", err);
console.log(
    drove
        ? "RESULT: PASS — JS context works, Claude was wrong"
        : "RESULT: FAIL — JS context is a dead snapshot, the C#-passing test cannot be written in mjs"
);
process.exit(drove ? 0 : 1);
