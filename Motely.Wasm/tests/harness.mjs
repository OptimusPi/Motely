/**
 * Boots motely-wasm once per test process (node --test isolates each file).
 * Embedded single-file build (empty BootsharpBinariesDirectory): the wasm + assemblies are
 * base64-inlined in resources.g.mjs, so boot() with no args picks them up automatically.
 *
 * The harness installs persistent event forwarders before boot; tests add their own listeners on
 * top and remove only their own. Everything is a named export off the package root because
 * RenameModule folds the whole API into the "index" module.
 *
 * Override the entry with MOTELY_WASM_ENTRY=/abs/path/to/dist/index.mjs.
 */
import { resolve, dirname } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const testsDir = dirname(fileURLToPath(import.meta.url));
const entry = process.env.MOTELY_WASM_ENTRY
    ? resolve(process.env.MOTELY_WASM_ENTRY)
    : resolve(testsDir, "..", "dist", "index.mjs");

const mod = await import(pathToFileURL(entry).href);
const bootsharp = mod.default;
const { MotelySearch, Jimmolate } = mod;

// --- pre-boot wiring ---------------------------------------------------------
// Persistent no-op subscribers keep the C# events wired for the whole process; tests add their
// own listeners on top (and remove only their own).
MotelySearch.onProgress.subscribe(() => {});
MotelySearch.onSeedMatch.subscribe(() => {});
MotelySearch.onScoredResult.subscribe(() => {});

await bootsharp.boot();
if (bootsharp.getStatus() !== bootsharp.BootStatus.Booted)
    throw new Error("boot: expected BootStatus.Booted");

export const harness = {
    bootsharp,
    ...mod,
    setFinder(fn) { Jimmolate.findSeed = fn; },
    resetFinder() { Jimmolate.findSeed = null; },
};
