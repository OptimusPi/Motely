/**
 * Boots motely-wasm once per process (ESM module cache).
 * Node: load bin/ via fs (file:// fetch fails). Browser consumers use HTTP bin/ URL.
 */
import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { loadBootResourcesFromDir } from "../../motely-wasm/dist/node-boot.mjs";

const testsDir = dirname(fileURLToPath(import.meta.url));
const wasmProjectDir = resolve(testsDir, "..");

const entryPath = process.env.MOTELY_WASM_ENTRY
    ? resolve(process.env.MOTELY_WASM_ENTRY)
    : resolve(wasmProjectDir, "..", "motely-wasm", "dist", "index.mjs");

const pkgRoot = resolve(dirname(entryPath), "..");
const binDir = resolve(pkgRoot, "bin");

async function createHarness() {
    const { default: bootsharp, Motely, ...enums } = await import(pathToFileURL(entryPath).href);

    const pkgVersion = JSON.parse(
        await readFile(resolve(pkgRoot, "package.json"), "utf8")
    ).version;

    Motely.reportWasmError = (message) => console.error("[WASM ERROR]", message);
    Motely.jimmolateProbe = () => false;

    await bootsharp.boot(await loadBootResourcesFromDir(binDir));

    if (bootsharp.getStatus() !== bootsharp.BootStatus.Booted) {
        throw new Error("boot: expected BootStatus.Booted");
    }

    return {
        bootsharp,
        Motely,
        pkgVersion,
        paths: { entryPath, pkgRoot, binDir, wasmProjectDir },
        ...enums,
    };
}

export const harness = await createHarness();
