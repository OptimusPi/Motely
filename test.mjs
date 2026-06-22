import bootsharp, { Jimmolate, Motely } from "./Motely.Wasm/dist/index.mjs";

// Jimmolate.probe must be bound before boot() — even if it's a no-op stub here.
Jimmolate.probe = (_seed, _deck, _stake) => true;

await bootsharp.boot();

console.log("Version:", Motely.getVersion());
console.log("Normalized:", Motely.normalizeSeed("abc123"));
