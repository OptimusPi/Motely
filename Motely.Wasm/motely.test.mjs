// Node smoke + regression suite for motely-wasm.
// Run after `dotnet publish Motely.Wasm`:  node motely.test.mjs
// CLAUDE.md publish gate depends on the final RESULT: PASS/FAIL line and the
// exit code — don't break that contract.
//
// Defaults to ../motely-wasm/dist/index.mjs. Override with MOTELY_WASM_ENTRY.

import { readFile } from "node:fs/promises";
import { resolve, dirname } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const entryPath = process.env.MOTELY_WASM_ENTRY
    ? resolve(process.env.MOTELY_WASM_ENTRY)
    : resolve(here, "..", "motely-wasm", "dist", "index.mjs");
const pkgRoot = resolve(dirname(entryPath), "..");
const { default: bootsharp, Motely } = await import(pathToFileURL(entryPath).href);

// `voucher: Any` and `joker: Any` inside should: are rejected by the parser
// (Motely.Tests JamlInvalidInputRejectionTests). Scoring fixtures must name specific identifiers.
const jaml = {
    must: `name: t
deck: Erratic
stake: Black
must:
  - joker: WeeJoker
    antes: [1]
`,
    anyMust: `name: t
deck: Red
stake: White
must:
  - joker: Any
    antes: [1]
`,
    scoring: `name: t
deck: Red
stake: White
should:
  - joker: WeeJoker
    antes: [1]
    score: 1
  - voucher: Telescope
    antes: [1, 2]
    score: 1
`,
    invalid: "not yaml !@#",
};

let pkgVersion;

async function boot() {
    pkgVersion = JSON.parse(await readFile(resolve(pkgRoot, "package.json"), "utf8")).version;
    const w = await readFile(resolve(pkgRoot, "bin", "dotnet.native.wasm"));
    await bootsharp.boot({ wasm: w.buffer.slice(w.byteOffset, w.byteOffset + w.byteLength) });
    if (bootsharp.getStatus() !== bootsharp.BootStatus.Booted) throw new Error("boot: not Booted");
}

// ── Tests ────────────────────────────────────────────────────────────────────

function testPublicApiSurface() {
    const required = [
        "version", "validateJaml", "explainJaml", "createPlan", "analyzeJamlSeeds",
        "createSearch", "createSearchSettings",
        "mountRoot", "unmountRoot", "pickRoot", "readTextFile", "writeTextFile",
        "onSeedMatch", "onScoredResult", "onProgress", "onFileChanges",
    ];
    const missing = required.filter(n => !(n in Motely));
    if (missing.length) throw new Error(`Motely missing: ${missing.join(", ")}`);
}

function testVersion_MatchesPackageJson() {
    const v = Motely.version();
    if (typeof v !== "string" || !/^\d+\.\d+\.\d+/.test(v)) throw new Error(`bad version: ${v}`);
    // FinalizeNpmPackage injects MotelyVersion into package.json post-pack; drift here = injection regressed.
    if (!v.startsWith(pkgVersion)) throw new Error(`assembly ${v} ≠ package.json ${pkgVersion}`);
}

function testEventContract() {
    // Bootsharp EventSubscriber contract — subscribe/unsubscribe/last on every documented event.
    for (const name of ["onSeedMatch", "onScoredResult", "onProgress", "onFileChanges"]) {
        const ev = Motely[name];
        if (typeof ev?.subscribe !== "function" || typeof ev?.unsubscribe !== "function" || !("last" in ev))
            throw new Error(`${name}: missing EventSubscriber contract`);
    }
}

function testValidateJaml() {
    if (Motely.validateJaml(jaml.must) !== "valid") throw new Error("valid JAML reported invalid");
    const err = Motely.validateJaml("not yaml !@#");
    if (typeof err !== "string" || err === "valid" || err.length === 0) throw new Error(`garbage should error: ${err}`);
}

function testExplainJaml() {
    const r = Motely.explainJaml(jaml.must);
    if (!r.startsWith("# JAML filter eval plan")) throw new Error(`bad header: ${r.slice(0, 80)}`);
    if (!r.includes("WeeJoker")) throw new Error(`should mention WeeJoker`);
    const errResult = Motely.explainJaml("not yaml !@#");
    if (typeof errResult !== "string" || !errResult.startsWith("# ERROR:"))
        throw new Error(`explainJaml(garbage) should return # ERROR: string, got: ${errResult?.slice(0, 80)}`);
}

function testCreatePlan() {
    const plan = Motely.createPlan(jaml.scoring);
    if (typeof plan?.scoredCsvHeaderQuoted !== "string") throw new Error("plan.scoredCsvHeaderQuoted missing");
    if (plan.scoreTallyColumnCount !== 2) throw new Error(`tally cols: ${plan.scoreTallyColumnCount}`);
    if (plan.tallyLabels?.length !== 2) throw new Error(`tally labels: ${plan.tallyLabels?.length}`);
}

function testAnalyzeJamlSeeds() {
    // Shape assertions matter — `{}` marshaling would silently lie here.
    const seeds = ["1AAAAAAA", "2BBBBBBB"];
    const result = Motely.analyzeJamlSeeds(jaml.anyMust, seeds);
    if (result.error != null) throw new Error(`unexpected error: ${result.error}`);
    if (result.seeds?.length !== 2) throw new Error(`seeds.length: ${result.seeds?.length}`);
    for (let i = 0; i < seeds.length; i++)
        if (result.seeds[i].seed !== seeds[i]) throw new Error(`order: seeds[${i}]`);
    const ante = result.seeds[0].analysis?.antes?.[0];
    if (!ante || !("boss" in ante) || !Array.isArray(ante.shopQueue) || !Array.isArray(ante.packs))
        throw new Error(`ante shape: ${JSON.stringify(ante)?.slice(0, 80)}`);
    // MotelyDeck.Red = 0, MotelyStake.White = 0 — deck/stake came from JAML, not defaulted.
    if (result.deck !== 0 || result.stake !== 0) throw new Error(`deck/stake not applied: ${result.deck}/${result.stake}`);
}

function testCreateSearchBuilder() {
    let threw = false;
    try { Motely.createSearch("not yaml !@#"); } catch { threw = true; }
    if (!threw) throw new Error("createSearch(garbage) should throw");
    if (typeof Motely.createSearchSettings()?.withSequentialSearch !== "function")
        throw new Error("createSearchSettings builder missing");
    const s = Motely.createSearch(jaml.scoring)
        .withSequentialSearch().withThreadCount(1).withProgressReportIntervalMs(0n);
    if (typeof s?.start !== "function") throw new Error("chained builder.start missing");
}

async function testListSearch_Completes() {
    const seeds = ["AAAAAAAA", "BBBBBBBB"];
    const search = Motely.createSearch(jaml.anyMust)
        .withListSearch(seeds, seeds.length).withThreadCount(1).start();
    await search.waitForCompletionAsync();
    if (!search.isCompleted) throw new Error("not completed");
    if (Number(search.totalSeedsSearched) !== 2) throw new Error(`searched: ${search.totalSeedsSearched}`);
    if (Number(search.matchingSeeds) < 1) throw new Error(`joker:Any should match: ${search.matchingSeeds}`);
}

async function testEvents_FireWithDocumentedShape() {
    // One scored search exercises onSeedMatch + onScoredResult + onProgress.
    // If a binding regresses to `{}`, the shape checks below catch it.
    const seeds = ["AAAAAAAA", "BBBBBBBB"];
    const matches = [], scored = [], progress = [];
    const onM = s => matches.push(s), onS = r => scored.push(r), onP = p => progress.push(p);
    Motely.onSeedMatch.subscribe(onM);
    Motely.onScoredResult.subscribe(onS);
    Motely.onProgress.subscribe(onP);
    try {
        const search = Motely.createSearch(jaml.scoring)
            .withListSearch(seeds, seeds.length).withThreadCount(1)
            .withProgressReportIntervalMs(0n).start();
        await search.waitForCompletionAsync();
    } finally {
        Motely.onSeedMatch.unsubscribe(onM);
        Motely.onScoredResult.unsubscribe(onS);
        Motely.onProgress.unsubscribe(onP);
    }
    if (matches.length === 0) throw new Error("onSeedMatch did not fire");
    if (typeof matches[0] !== "string" || !matches[0]) throw new Error(`onSeedMatch payload: ${matches[0]}`);
    if (scored.length === 0) throw new Error("onScoredResult did not fire");
    const r = scored[0];
    if (typeof r.seed !== "string" || typeof r.score !== "number" || !Array.isArray(r.tallies) || r.tallies.length !== 2)
        throw new Error(`onScoredResult shape: ${JSON.stringify(r)?.slice(0, 100)}`);
    if (progress.length === 0) throw new Error("onProgress did not fire");
    const p = progress.at(-1);
    if (typeof p.percentComplete !== "number" || typeof p.seedsPerMillisecond !== "number")
        throw new Error(`onProgress numbers: ${JSON.stringify(p)?.slice(0, 100)}`);
    // long → BigInt; if these come back as number or `{}` the binding regressed.
    if (typeof p.seedsSearched !== "bigint" || typeof p.elapsedMilliseconds !== "bigint")
        throw new Error(`onProgress bigints: ${JSON.stringify(p)?.slice(0, 100)}`);
}

async function testCancel_CompletesCleanly() {
    const search = Motely.createSearch(jaml.must).withSequentialSearch().withThreadCount(1).start();
    search.cancel();
    await search.waitForCompletionAsync();
    if (!search.isCompleted) throw new Error("cancelled search not completed");
}

function testBootStatus_StillBooted() {
    if (bootsharp.getStatus() !== bootsharp.BootStatus.Booted) throw new Error("post-suite boot status drifted");
}

// ── Runner ───────────────────────────────────────────────────────────────────

const tests = [
    testPublicApiSurface, testVersion_MatchesPackageJson, testEventContract,
    testValidateJaml, testExplainJaml, testCreatePlan, testAnalyzeJamlSeeds,
    testCreateSearchBuilder, testListSearch_Completes, testEvents_FireWithDocumentedShape,
    testCancel_CompletesCleanly, testBootStatus_StillBooted,
];

try { await boot(); console.log(`boot: BootStatus.Booted (package ${pkgVersion})`); }
catch (e) { console.error(`BOOT FAILED: ${e?.stack ?? e}`); process.exit(1); }

let passed = 0, failed = 0;
const failures = [];
for (const t of tests) {
    try { await t(); console.log(`pass: ${t.name}`); passed++; }
    catch (e) { console.error(`fail: ${t.name}\n       ${e?.message ?? e}`); failures.push({ name: t.name, msg: e?.message ?? String(e) }); failed++; }
}

console.log(`\n${passed}/${tests.length} passed, ${failed} failed`);
if (failed > 0) for (const f of failures) console.log(`  - ${f.name}: ${f.msg}`);
console.log(failed === 0 ? "RESULT: PASS" : "RESULT: FAIL");
process.exit(failed === 0 ? 0 : 1);
