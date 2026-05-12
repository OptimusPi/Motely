// Single-seed analyzer against motely-wasm. Spirit of Jamlyzer: JAML wires deck/stake (and shop-
// match highlights) straight into the analyzer — no separate config surface.
//
// Defaults to O6LA1511 under JamlFilters/SpeedTest.jaml. Both args are optional.
// Writes the full SeedAnalysisDto JSON to verify.txt and echoes Ante 1 tags + boss to stdout.
//
// Run from repo root:  node Motely.Wasm/analyzer.mjs [SEED] [JAML_PATH]
// Examples:
//   node Motely.Wasm/analyzer.mjs
//   node Motely.Wasm/analyzer.mjs R9XL1G11
//   node Motely.Wasm/analyzer.mjs O6LA1511 JamlFilters/PerkeoObservatory.jaml

import { readFileSync, writeFileSync } from "node:fs";
import { readFile as readFileAsync } from "node:fs/promises";
import { fileURLToPath, pathToFileURL } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..");
const wasmDir = resolve(repoRoot, "motely-wasm");
const indexPath = resolve(wasmDir, "index.mjs");
const rootBindingPath = resolve(wasmDir, "generated", "index.g.mjs");
const wasmBinaryPath = resolve(wasmDir, "bin", "dotnet.native.wasm");
const verifyPath = resolve(here, "verify.txt");

const seed = (process.argv[2] ?? "O6LA1511").trim();
const jamlArg = process.argv[3] ?? "JamlFilters/SpeedTest.jaml";
const jamlPath = resolve(repoRoot, jamlArg);
const yaml = readFileSync(jamlPath, "utf8");

const bootsharp = (await import(pathToFileURL(indexPath).href)).default;
const { Program } = await import(pathToFileURL(rootBindingPath).href);

const wasm = (await readFileAsync(wasmBinaryPath)).buffer;
await bootsharp.boot({ wasm, assemblies: [], icu: [], symbols: [], pdb: [] });

if (typeof Program.analyze !== "function") {
    console.error("Program.analyze is not exported. Rebuild Motely.Wasm: dotnet publish Motely.Wasm -c Release");
    process.exit(2);
}

const json = Program.analyze(seed, yaml);
writeFileSync(verifyPath, json, "utf8");

const dto = JSON.parse(json);
const ante1 = dto.antes?.find(a => a.ante === 1);

console.log(`seed:  ${dto.seed}`);
console.log(`deck:  ${dto.deck}  stake: ${dto.stake}`);
console.log(`jaml:  ${jamlArg}`);
console.log(`wrote: ${verifyPath} (${json.length} bytes)`);
console.log("");

if (!ante1) {
    console.error("No Ante 1 in analysis output.");
    process.exit(1);
}

// Hard-coded expectation only fires for the default (O6LA1511 @ SpeedTest.jaml). Any other combo
// just prints the ante-1 trio for eyeball verification — no synthetic pass/fail noise.
const isDefaultPair = seed === "O6LA1511" && jamlArg.replace(/\\/g, "/").endsWith("SpeedTest.jaml");

if (isDefaultPair) {
    // DTO emits display strings ("Foil Tag", "The Empress") while JAML uses PascalCase
    // ("FoilTag", "TheEmpress"). Compare whitespace-insensitively so either form passes.
    const stripWS = s => String(s).replace(/\s+/g, "");
    const shop0 = ante1.shopQueue?.[0];
    const shop1 = ante1.shopQueue?.[1];

    const rows = [
        ["smallBlindTag",      ante1.smallBlindTag,    "FoilTag"],
        ["bigBlindTag",        ante1.bigBlindTag,      "JuggleTag"],
        ["boss",               ante1.boss,             "TheClub"],
        ["voucher",            ante1.voucher,          "Hieroglyph"],
        ["shopQueue[0].name",  shop0?.name,            "TurtleBean"],
        ["shopQueue[1].name",  shop1?.name,            "TheEmpress"],
    ];

    let failed = 0;
    for (const [field, actual, exp] of rows) {
        const ok = stripWS(actual) === stripWS(exp);
        if (!ok) failed++;
        console.log(`${ok ? "PASS" : "FAIL"}: ante1.${field} = ${JSON.stringify(actual)}  (~= ${JSON.stringify(exp)})`);
    }

    process.exit(failed === 0 ? 0 : 1);
} else {
    console.log(`ante1.voucher       = ${JSON.stringify(ante1.voucher)}`);
    console.log(`ante1.smallBlindTag = ${JSON.stringify(ante1.smallBlindTag)}`);
    console.log(`ante1.bigBlindTag   = ${JSON.stringify(ante1.bigBlindTag)}`);
    console.log(`ante1.boss          = ${JSON.stringify(ante1.boss)}`);
    console.log(`ante1.shopQueue[0]  = ${JSON.stringify(ante1.shopQueue?.[0]?.name)}`);
    console.log(`ante1.shopQueue[1]  = ${JSON.stringify(ante1.shopQueue?.[1]?.name)}`);
}
