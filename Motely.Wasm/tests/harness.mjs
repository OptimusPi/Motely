/**
 * Boots motely-wasm once per test process (node --test isolates each file).
 * Embedded single-file build (empty BootsharpBinariesDirectory): the wasm + assemblies are
 * base64-inlined in resources.g.mjs, so boot() with no args picks them up automatically.
 *
 * Bootsharp wiring order (see docs/guide/getting-started.md): [Import] bindings must be assigned
 * and [Export] events subscribed BEFORE boot() — that's when the C#↔JS bridge is established.
 * So the harness installs persistent event forwarders and the Jimmolate import binding here,
 * pre-boot; tests then add their own listeners on the same events and swap the finder via
 * setFinder(). Everything is a named export off the package root because RenameModule folds the
 * whole API into the "index" module.
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

// Bind the Jimmolate finder import once, delegating to a swappable function (default: keep all).
let finder = () => true;
Jimmolate.findSeed = (seed, deck, stake) => finder(seed, deck, stake);

await bootsharp.boot();
if (bootsharp.getStatus() !== bootsharp.BootStatus.Booted)
    throw new Error("boot: expected BootStatus.Booted");

export const harness = {
    bootsharp,
    ...mod,
    /** Swap the Jimmolate finder the bound import delegates to. */
    setFinder(fn) { finder = fn; },
    /** Restore the default keep-all finder. */
    resetFinder() { finder = () => true; },
};
