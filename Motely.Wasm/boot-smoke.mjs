// Bare boot smoke: does Bootsharp's DEFAULT publish output boot in Node with no glue?
import bootsharp, { Motely } from "./bin/bootsharp/index.mjs";

Motely.reportWasmError = (m) => console.error("[WASM ERROR]", m);

await bootsharp.boot();

console.log("BOOT OK — status:", bootsharp.getStatus());
console.log("parseJaml smoke:", !!Motely.parseJaml("must:\n  - joker: Blueprint\n"));
