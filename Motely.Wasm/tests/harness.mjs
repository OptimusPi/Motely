/**
 * Boots motely-wasm once per process (ESM module cache).
 * Sideloaded build (BootsharpBinariesDirectory → dist/bin): the .wasm is a separate
 * file, not inlined. node's fetch can't read file:// URLs, so we hand boot() the bytes
 * directly as a BootResources object instead of letting it fetch a root URL.
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

    // NativeAOT-LLVM is single-file, so only `wasm` is needed (the other manifest arrays
    // are empty). resolveBinary() in config.mjs takes the bytes as-is when not a string.
    const wasm = await readFile(resolve(dirname(entryPath), "bin", bootsharp.manifest.wasm));
    await bootsharp.boot({ wasm });

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
