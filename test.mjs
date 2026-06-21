import bootsharp, { Program } from "./Motely.Wasm/bin/motely-wasm/index.mjs";

await bootsharp.boot();

console.log("Version:", Program.getVersion());
console.log("Normalized:", Program.normalizeSeed("abc123"));
