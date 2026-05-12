// End-to-end smoke against motely-wasm with the JAML Search wrapper.
//
// L1 (structural): ES module imports, version stamped, bootsharp surface.
// L2 (boot):       AOT-LLVM wasm instantiates, Main() runs, status -> Booted.
// L3 (interop):    Program.search(yamlString) returns string[] of matches.
//
// Uses JamlFilters/SpeedTest.jaml because its seeds: [...] block bounds the
// search to a curated list — finishes fast, no full-space crawl.
//
// Run from repo root:  node Motely.Wasm/smoke.mjs

import { readFileSync } from "node:fs";
import { readFile as readFileAsync } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..");
const wasmDir = resolve(repoRoot, "motely-wasm");
const pkgPath = resolve(wasmDir, "package.json");
const indexPath = resolve(wasmDir, "index.mjs");
const rootBindingPath = resolve(wasmDir, "generated", "index.g.mjs");
const wasmBinaryPath = resolve(wasmDir, "bin", "dotnet.native.wasm");
const jamlPath = resolve(repoRoot, "JamlFilters", "SpeedTest.jaml");

const pkg = JSON.parse(readFileSync(pkgPath, "utf8"));
const yaml = readFileSync(jamlPath, "utf8");
const bootsharp = (await import(pathToFileUrl(indexPath))).default;
const { Program } = await import(pathToFileUrl(rootBindingPath));

const expected = process.argv[2] ?? pkg.version;

const checks = [];

// --- L1: structural ---------------------------------------------------------
checks.push([pkg.name === "motely-wasm",                  `L1 package name: ${pkg.name}`]);
checks.push([pkg.version === expected,                    `L1 version: ${pkg.version} (expected ${expected})`]);
checks.push([typeof bootsharp?.boot === "function",       `L1 bootsharp.boot is a function`]);
checks.push([typeof Program?.search === "function",       `L1 Program.search binding emitted`]);

// --- L2: boot ---------------------------------------------------------------
let bootError = null;
try {
    const wasm = (await readFileAsync(wasmBinaryPath)).buffer;
    await bootsharp.boot({ wasm, assemblies: [], icu: [], symbols: [], pdb: [] });
} catch (err) {
    bootError = err;
}

checks.push([bootError === null,
    bootError === null
        ? `L2 boot() resolved`
        : `L2 boot() threw: ${bootError?.message ?? bootError}`]);

if (bootError === null) {
    checks.push([bootsharp.getStatus() === bootsharp.BootStatus.Booted,
        `L2 getStatus() === Booted`]);

    // --- L3: real JAML search ----------------------------------------------
    let result = null;
    let searchError = null;
    const t0 = performance.now();
    try { result = Program.search(yaml); } catch (err) { searchError = err; }
    const elapsed = (performance.now() - t0).toFixed(1);

    checks.push([searchError === null,
        searchError === null
            ? `L3 Program.search(yaml) returned in ${elapsed}ms`
            : `L3 Program.search(yaml) threw: ${searchError?.message ?? searchError}`]);
    checks.push([Array.isArray(result),
        `L3 result is an Array (got ${typeof result})`]);
    if (Array.isArray(result)) {
        checks.push([result.every(s => typeof s === "string"),
            `L3 result is string[] (length=${result.length})`]);
        if (result.length > 0)
            console.log(`       sample hits: ${result.slice(0, 5).join(", ")}${result.length > 5 ? ", ..." : ""}`);
    }
}

// --- report -----------------------------------------------------------------
let failed = 0;
for (const [ok, label] of checks) {
    console.log(`${ok ? "PASS" : "FAIL"}: ${label}`);
    if (!ok) failed++;
}

process.exit(failed === 0 ? 0 : 1);

function pathToFileUrl(p) {
    const norm = p.replace(/\\/g, "/");
    return norm.startsWith("/") ? "file://" + norm : "file:///" + norm;
}
