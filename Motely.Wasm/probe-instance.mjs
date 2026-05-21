import { createRequire } from "node:module";
import { readFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import path from "node:path";

const require = createRequire(import.meta.url);
const pkgDir = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    "../motely-wasm"
);
const wasmPath = path.join(pkgDir, "bin/dotnet.native.wasm");
const wasmBytes = await readFile(wasmPath);

const bootsharp = (await import(path.join(pkgDir, "dist/index.mjs"))).default;
const { Motely } = await import(path.join(pkgDir, "dist/generated/index.g.mjs"));
const { MotelyDeck, MotelyStake } = await import(
    path.join(pkgDir, "dist/generated/motely/enums.g.mjs")
);
const { MotelyStreamKind } = await import(
    path.join(pkgDir, "dist/generated/motely.g.mjs")
);

await bootsharp.boot({
    wasm: wasmBytes.buffer.slice(
        wasmBytes.byteOffset,
        wasmBytes.byteOffset + wasmBytes.byteLength
    ),
});

const router = Motely.createSeedRouter("AAAAAAAA", MotelyDeck.Red, MotelyStake.White);
const ctx = router.instance();
const keys = ctx && typeof ctx === "object" ? Object.keys(ctx) : [];
const hasCreateShop =
    ctx && typeof ctx.createShopItemStream === "function";

const cursor = Motely.createStreamCursor(
    "AAAAAAAA",
    MotelyDeck.Red,
    MotelyStake.White,
    1,
    MotelyStreamKind.Shop
);
const chunk = cursor.getNextChunk(3);

router.dispose();

console.log("router.instance() typeof:", typeof ctx);
console.log("router.instance() JSON:", JSON.stringify(ctx));
console.log("router.instance() keys:", keys);
console.log("router.instance() createShopItemStream:", hasCreateShop);
console.log("createStreamCursor getNextChunk(3):", Array.from(chunk));
console.log(
    chunk.every((n) => typeof n === "number") ? "STREAM_OK" : "STREAM_BAD"
);
