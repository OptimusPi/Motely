// Node smoke for motely-wasm. Mirrors Motely.Tests/AnalyzerUnitTests.cs in JS.
// Run: `node test-sanity.mjs` from this directory after `dotnet publish Motely.Wasm`.

import { readFile } from "node:fs/promises";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import bootsharp, { Motely } from "../motely-wasm/dist/index.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const pkgRoot = resolve(here, "..", "motely-wasm");

const SAMPLE_JAML = `
name: SmokeTest
deck: Erratic
stake: Black
must:
  - joker: WeeJoker
    antes: [1]
`;

// Mirrors AnalyzerUnitTests.TestJamlyzerAnalyzeSeeds_AttachesStructuredSeedAnalysis.
const ANALYZE_JAML = `
name: test
deck: Red
stake: White
should:
  - joker: Any
    score: 1
`;

let pkgVersion;

async function boot() {
    const pkgJson = JSON.parse(await readFile(resolve(pkgRoot, "package.json"), "utf8"));
    pkgVersion = pkgJson.version;
    const wasmBytes = await readFile(resolve(pkgRoot, "bin", "dotnet.native.wasm"));
    await bootsharp.boot({
        wasm: wasmBytes.buffer.slice(
            wasmBytes.byteOffset,
            wasmBytes.byteOffset + wasmBytes.byteLength
        )
    });
    if (bootsharp.getStatus() !== bootsharp.BootStatus.Booted) {
        throw new Error(`expected BootStatus.Booted, got ${bootsharp.getStatus()}`);
    }
}

function testVersion_MatchesNpmPackageJson() {
    const v = Motely.version();
    if (typeof v !== "string" || !/^\d+\.\d+\.\d+/.test(v)) {
        throw new Error(`expected semver-ish string, got ${JSON.stringify(v)}`);
    }
    if (!v.startsWith(pkgVersion)) {
        throw new Error(`assembly version ${v} does not match package.json ${pkgVersion} — FinalizeNpmPackage injection drifted`);
    }
}

function testValidateJaml_ValidInput_ReturnsValid() {
    const result = Motely.validateJaml(SAMPLE_JAML);
    if (result !== "valid") throw new Error(`expected 'valid', got ${JSON.stringify(result)}`);
}

function testValidateJaml_GarbageInput_ReturnsErrorString() {
    const result = Motely.validateJaml("not yaml at all !@#");
    if (result === "valid") throw new Error("expected error string, got 'valid'");
    if (typeof result !== "string" || result.length === 0) {
        throw new Error(`expected non-empty error string, got ${JSON.stringify(result)}`);
    }
}

function testExplainJaml_StartsWithDocumentedHeader() {
    // JamlSearchBuilder.ExplainPlan writes "# JAML filter eval plan" as line 1.
    const result = Motely.explainJaml(SAMPLE_JAML);
    if (typeof result !== "string") throw new Error(`expected string, got ${typeof result}`);
    if (!result.startsWith("# JAML filter eval plan")) {
        throw new Error(`expected header '# JAML filter eval plan', got: ${result.slice(0, 80)}`);
    }
}

function testCreatePlan_HasJamlSearchPlanFields() {
    // JamlSearchPlan record: ScoreTallyColumnCount, ScoredCsvHeaderQuoted, TallyLabels.
    const plan = Motely.createPlan(SAMPLE_JAML);
    if (!plan || typeof plan !== "object") throw new Error(`expected object, got ${typeof plan}`);
    if (typeof plan.scoreTallyColumnCount !== "number") {
        throw new Error(`scoreTallyColumnCount: expected number, got ${typeof plan.scoreTallyColumnCount}`);
    }
    if (typeof plan.scoredCsvHeaderQuoted !== "string") {
        throw new Error(`scoredCsvHeaderQuoted: expected string, got ${typeof plan.scoredCsvHeaderQuoted}`);
    }
    if (!Array.isArray(plan.tallyLabels)) {
        throw new Error(`tallyLabels: expected array, got ${typeof plan.tallyLabels}`);
    }
}

function testAnalyzeJamlSeeds_KnownSeed_AttachesStructuredAnalysis() {
    // Mirrors AnalyzerUnitTests.TestJamlyzerAnalyzeSeeds_AttachesStructuredSeedAnalysis.
    const result = Motely.analyzeJamlSeeds(ANALYZE_JAML, ["1AAAAAAA"]);
    if (result.error !== null && result.error !== undefined) {
        throw new Error(`expected null error, got ${JSON.stringify(result.error)}`);
    }
    if (!Array.isArray(result.seeds) || result.seeds.length !== 1) {
        throw new Error(`expected 1 seed, got ${result.seeds?.length}`);
    }
    const seed = result.seeds[0];
    if (seed.seed !== "1AAAAAAA") throw new Error(`expected seed '1AAAAAAA', got ${seed.seed}`);
    if (!seed.analysis) throw new Error(`expected analysis object, got ${seed.analysis}`);
    if (!Array.isArray(seed.analysis.antes) || seed.analysis.antes.length === 0) {
        throw new Error(`expected non-empty antes array, got ${JSON.stringify(seed.analysis.antes)?.slice(0, 80)}`);
    }
    const ante1 = seed.analysis.antes[0];
    if (!("boss" in ante1)) throw new Error(`expected 'boss' field on antes[0], got keys ${Object.keys(ante1).join(",")}`);
}

function testPublicApiSurface_DocumentedMembersPresent() {
    const required = [
        "validateJaml",
        "explainJaml",
        "createPlan",
        "analyzeJamlSeeds",
        "createSearch",
        "version",
        "onSeedMatch",
        "onScoredResult",
        "onProgress",
    ];
    const missing = required.filter((name) => !(name in Motely));
    if (missing.length > 0) {
        throw new Error(`Motely is missing documented members: ${missing.join(", ")}`);
    }
}

const tests = [
    testVersion_MatchesNpmPackageJson,
    testValidateJaml_ValidInput_ReturnsValid,
    testValidateJaml_GarbageInput_ReturnsErrorString,
    testExplainJaml_StartsWithDocumentedHeader,
    testCreatePlan_HasJamlSearchPlanFields,
    testAnalyzeJamlSeeds_KnownSeed_AttachesStructuredAnalysis,
    testPublicApiSurface_DocumentedMembersPresent,
];

let passed = 0;
let failed = 0;

try {
    await boot();
    console.log(`boot: BootStatus.Booted (package ${pkgVersion})`);
} catch (e) {
    console.error(`BOOT FAILED: ${e?.stack ?? e}`);
    process.exit(1);
}

for (const test of tests) {
    try {
        test();
        console.log(`pass: ${test.name}`);
        passed++;
    } catch (e) {
        console.error(`fail: ${test.name}\n       ${e?.message ?? e}`);
        failed++;
    }
}

console.log(`\n${passed}/${tests.length} passed, ${failed} failed`);
console.log(failed === 0 ? "RESULT: PASS" : "RESULT: FAIL");
process.exit(failed === 0 ? 0 : 1);
