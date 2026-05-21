/**
 * Node/Bun/Deno boot resources loader. Node fetch() does not load file:// bin URLs;
 * use this before bootsharp.boot() in Node — same manifest as fetchResources().
 */
import { readFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import manifest from "./generated/resources.g.mjs";

async function readResource(binDir, name) {
    const bytes = await readFile(join(binDir, name));
    return {
        name,
        content: bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength),
    };
}

/** @param {string} binDir Absolute path to the package bin/ directory. */
export async function loadBootResourcesFromDir(binDir) {
    const [wasm, assemblies, icu, symbols, pdb] = await Promise.all([
        readFile(join(binDir, manifest.wasm)).then((bytes) =>
            bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength)
        ),
        Promise.all(manifest.assemblies.map((n) => readResource(binDir, n))),
        Promise.all(manifest.icu.map((n) => readResource(binDir, n))),
        Promise.all(manifest.symbols.map((n) => readResource(binDir, n))),
        Promise.all(manifest.pdb.map((n) => readResource(binDir, n))),
    ]);
    return { wasm, assemblies, icu, symbols, pdb };
}

/** @param {string} [packageName] npm package name (default motely-wasm). */
export function resolvePackageBinDir(packageName = "motely-wasm") {
    const pkgJson = fileURLToPath(import.meta.resolve(`${packageName}/package.json`));
    return join(dirname(pkgJson), "bin");
}
