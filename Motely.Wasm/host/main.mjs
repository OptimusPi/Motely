// Boots the Bootsharp ES module and hangs the exported API off globalThis.motely.
// The compiled module lives at ../bin/motely-wasm (dotnet publish Motely.Wasm);
// binaries are embedded, so boot() needs no arguments and no extra files served.
import bootsharp from "../bin/motely-wasm/index.mjs";
import { MotelyWasmApi } from "../bin/motely-wasm/generated/modules/motely/wasm.g.mjs";

await bootsharp.boot();

// Async exports return plain Task and results are collected with the sync takeRun() —
// Bootsharp packs serialized returns into an Int64 and the runtime cannot marshal
// Task<Int64> (see MotelyWasmApi remarks). Recompose the pair here so pages write
// one awaited call and never see the seam.
globalThis.motely = {
  ...MotelyWasmApi,
  scoreSeeds: async (jaml, seeds) => {
    await MotelyWasmApi.runScoreSeeds(jaml, seeds);
    return MotelyWasmApi.takeRun();
  },
  findSeeds: async (jaml, intent) => {
    await MotelyWasmApi.runFindSeeds(jaml, intent);
    return MotelyWasmApi.takeRun();
  },
};
globalThis.dispatchEvent(new CustomEvent("motely-ready"));
