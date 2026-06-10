/**
 * Boots motely-wasm once per process (ESM module cache).
 * Embedded build (BootsharpBinariesDirectory empty): wasm + assemblies are
 * base64-inlined in resources.g.mjs. boot() with no args uses the embedded
 * resources automatically.
 */
import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const testsDir = dirname(fileURLToPath(import.meta.url));
const wasmProjectDir = resolve(testsDir, "..");

const entryPath = process.env.MOTELY_WASM_ENTRY
    ? resolve(process.env.MOTELY_WASM_ENTRY)
    : resolve(wasmProjectDir, "..", "motely-wasm", "dist", "index.mjs");

const pkgRoot = resolve(dirname(entryPath), "..");

async function createHarness() {
    const generatedDir = resolve(dirname(entryPath), "generated", "modules", "motely");

    const { default: bootsharp } = await import(pathToFileURL(entryPath).href);
    // The public API object is `Program` (C# class Motely.Wasm.Program), emitted under the
    // `motely/wasm` module. It is NOT re-exported from the root barrel — index.g.mjs is `export {}`.
    const { Program: Motely } = await import(
        pathToFileURL(resolve(generatedDir, "wasm.g.mjs")).href
    );
    // Enums (MotelyDeck, MotelyStake, ...) live in the `motely/enums` module, also not at root.
    const enums = await import(pathToFileURL(resolve(generatedDir, "enums.g.mjs")).href);

    const pkgVersion = JSON.parse(
        await readFile(resolve(pkgRoot, "package.json"), "utf8")
    ).version;

    Motely.reportWasmError = (message) => console.error("[WASM ERROR]", message);

    // Embedded build: wasm is inlined in resources.g.mjs. boot() with no args
    // picks it up via fetchResources() → embedded branch.
    await bootsharp.boot();

    if (bootsharp.getStatus() !== bootsharp.BootStatus.Booted) {
        throw new Error("boot: expected BootStatus.Booted");
    }

    return {
        bootsharp,
        Motely,
        pkgVersion,
        paths: { entryPath, pkgRoot, wasmProjectDir },
        ...enums,
    };
}

export const harness = await createHarness();
