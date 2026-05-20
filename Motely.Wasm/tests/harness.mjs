/**
 * Boots motely-wasm once per process (ESM module cache).
 * Node path matches motely-wasm README and Bootsharp getting-started (wasm bytes, not URL).
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
    const { default: bootsharp, Motely } = await import(pathToFileURL(entryPath).href);

    const enums = await import(
        pathToFileURL(resolve(pkgRoot, "dist", "generated", "motely", "enums.g.mjs")).href
    );
    const { MotelyStreamKind } = await import(
        pathToFileURL(resolve(pkgRoot, "dist", "generated", "motely.g.mjs")).href
    );

    const pkgVersion = JSON.parse(
        await readFile(resolve(pkgRoot, "package.json"), "utf8")
    ).version;

    const wasmFile = await readFile(resolve(pkgRoot, "bin", "dotnet.native.wasm"));
    await bootsharp.boot({
        wasm: wasmFile.buffer.slice(
            wasmFile.byteOffset,
            wasmFile.byteOffset + wasmFile.byteLength
        ),
    });

    if (bootsharp.getStatus() !== bootsharp.BootStatus.Booted) {
        throw new Error("boot: expected BootStatus.Booted");
    }

    // Default so C# does not fault if EvalJimmolate runs outside jimmolate tests.
    Motely.evalJimmolate = () => true;

    return {
        bootsharp,
        Motely,
        pkgVersion,
        paths: { entryPath, pkgRoot, wasmProjectDir },
        ...enums,
        MotelyStreamKind,
    };
}

export const harness = await createHarness();
