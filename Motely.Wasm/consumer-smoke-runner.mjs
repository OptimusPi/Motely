// Consumer smoke fixture — real code, not a string blob.
//
// pack-consumer-smoke.mjs copies this file into a freshly-installed consumer
// directory and runs it there, so the bare `motely-wasm` import resolves to the
// packed-and-installed tarball (the thing a real consumer gets), NOT the repo
// workspace. It is not meant to be run in place.
import bootsharp, { Motely } from "motely-wasm";
import { readFile } from "node:fs/promises";

await bootsharp.boot();

const jaml = `
name: smoke
deck: Red
stake: White
must:
  - joker: WeeJoker
    antes: [1]
`;

const cfg = Motely.fromJaml(jaml);
cfg.seeds = ["AAAAAAAA"];
const result = Motely.runSeedListSearch(cfg);
if (!result.isCompleted) throw new Error("search did not complete");

const pkg = JSON.parse(
    await readFile(
        new URL("package.json", import.meta.resolve("motely-wasm/package.json")),
        "utf8",
    ),
);
console.log("CONSUMER_SMOKE: PASS", pkg.version);
