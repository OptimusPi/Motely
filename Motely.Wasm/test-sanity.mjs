// Node smoke test for motely-wasm.
//
// Structured to mirror Motely.Tests/AnalyzerUnitTests.cs xUnit shape: one
// named test function per case, arrange/act/assert sections, a runner at
// the bottom that reports per-test pass/fail. The C# test surface is the
// contract — this file shadows it for the JS consumer.
//
// Why Node + preloaded ArrayBuffer (not fetch a URL): per BOOTSHARP.md and
// the README's Boot table, Node `fetch()` cannot reliably load `file://`
// for binary resources, so server-side consumers preload the wasm bytes
// and pass them via `boot({ wasm: ArrayBuffer })`. This mirrors what a
// Node consumer would do; jaml-ui's browser worker uses the URL form.
//
// Covered:
//   - validateJaml (good + bad input)
//   - explainJaml
//   - createPlan
//   - public API surface presence (regression guard against accidentally
//     dropping a documented method — including createSearch, which is the
//     entry point Jimmolate will plug into once it's surfaced in JS)
//
// Not covered (next session):
//   - createSearch().withSequentialSearch().start() lifecycle — needs cancel
//     to avoid hanging Node; deserves its own test file once we wire it.
//   - Jimmolate (C# core has it via Motely/Filters/Native/JimmolateFilterDesc.cs
//     + Motely.Tests/JimmolateFilterDescTests.cs; JS surface in Motely.Wasm
//     doesn't expose a delegate-passing entry point yet).
//   - File-system mount round-trip — browser-only (OPFS); use test-browser.html.
//
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

// ---- one-time setup ---------------------------------------------------------

async function boot() {
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

// ---- tests ------------------------------------------------------------------

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

function testExplainJaml_ReturnsNonEmptyString() {
    const result = Motely.explainJaml(SAMPLE_JAML);
    if (typeof result !== "string" || result.length === 0) {
        throw new Error(`expected non-empty string, got ${JSON.stringify(result)?.slice(0, 80)}`);
    }
}

function testCreatePlan_ReturnsObject() {
    const plan = Motely.createPlan(SAMPLE_JAML);
    if (!plan || typeof plan !== "object") {
        throw new Error(`expected plan object, got ${typeof plan}`);
    }
}

// Regression guard: the README documents this surface. If any of these go
// missing, downstream consumers (jaml-ui, jaml-mcp) break silently at the
// next major. Also: Jimmolate will live behind createSearch when it lands
// in JS — keep this assert green and the next session's wiring has a hook.
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

// ---- runner -----------------------------------------------------------------

const tests = [
    testValidateJaml_ValidInput_ReturnsValid,
    testValidateJaml_GarbageInput_ReturnsErrorString,
    testExplainJaml_ReturnsNonEmptyString,
    testCreatePlan_ReturnsObject,
    testPublicApiSurface_DocumentedMembersPresent,
];

let passed = 0;
let failed = 0;

try {
    await boot();
    console.log(`boot: BootStatus.Booted`);
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
