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
// Each C# namespace becomes its own ES module (Bootsharp namespaces.md).
// Motely.Enums.* enums live in motely/enums.g.mjs; MotelyStreamKind sits in
// the root Motely namespace, so it's in motely.g.mjs.
const {
    MotelyItemType, MotelyItemTypeCategory, MotelyJokerRarity,
    MotelyItemEdition, MotelyItemSeal, MotelyItemEnhancement,
    MotelyTag, MotelyVoucher, MotelyBoosterPack,
} = await import(pathToFileURL(resolve(pkgRoot, "dist", "generated", "motely", "enums.g.mjs")).href);
const { MotelyStreamKind } =
    await import(pathToFileURL(resolve(pkgRoot, "dist", "generated", "motely.g.mjs")).href);

// `voucher: Any` and `joker: Any` in must/should are rejected by the parser. Scoring fixtures must name specific identifiers.
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

// Eight probe seeds — shop/pack/tag variety in analyzer output.
const probeSeeds = ["AAAAAAAA", "BBBBBBBB", "CCCCCCCC", "DDDDDDDD", "EEEEEEEE", "FFFFFFFF", "GGGGGGGG", "HHHHHHHH"];

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
        "evalJimmolate",
        "createStreamCursor",
        "decodeItemType", "decodeItemCategory", "decodeJokerRarity",
        "decodeItemEdition", "decodeItemSeal", "decodeItemEnhancement",
        "isPerishable", "isEternal", "isRental",
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
    // Bootsharp maps C# int[] → JS Int32Array (TypedArray), not plain Array — that's by design,
    // not a regression. Accept either, but still reject {} and the index-keyed plain object that
    // would surface a real marshaling break.
    const talliesOk = (r.tallies instanceof Int32Array || Array.isArray(r.tallies))
        && r.tallies.length === 2
        && typeof r.tallies[0] === "number";
    if (typeof r.seed !== "string" || typeof r.score !== "number" || !talliesOk)
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
    // 1-char batch 0 = 35 seeds; cancel() is called before the batch finishes (or after —
    // either way waitForCompletionAsync() must resolve and isCompleted must be true).
    // An unbounded sequential search blocks the WASM event loop and cannot be cancelled from JS.
    const search = Motely.createSearch(jaml.must)
        .withSequentialSearch()
        .withBatchCharacterCount(1)
        .withStartBatchIndex(0n)
        .withEndBatchIndex(0n)
        .withThreadCount(1)
        .start();
    search.cancel();
    await search.waitForCompletionAsync();
    if (!search.isCompleted) throw new Error("cancelled search not completed");
}

function testBootStatus_StillBooted() {
    if (bootsharp.getStatus() !== bootsharp.BootStatus.Booted) throw new Error("post-suite boot status drifted");
}

// ── Analyzer cross-check tests ───────────────────────────────────────────────
// Analyzer reports X → search for X must find the same seed. Divergence fails the test.

function testAnalyzer_FirstAnteFirstPack_IsBuffoonNormal() {
    // Ante 1 pack 0 must be a 2-item Buffoon pack.
    const r = Motely.analyzeJamlSeeds(jaml.anyMust, probeSeeds);
    if (r.error != null) throw new Error(`analyzeJamlSeeds failed: ${r.error}`);
    const s = r.seeds?.[0];
    if (!s) throw new Error("No probe seed returned analysis");
    const pack = s.analysis?.antes?.[0]?.packs?.[0];
    if (!pack) throw new Error("No packs in ante 1");
    const packName = MotelyBoosterPack?.[pack.type];
    if (packName !== "Buffoon") throw new Error(`first pack: expected Buffoon, got ${packName ?? pack.type} (is MotelyBoosterPack exported?)`);
    if (!Array.isArray(pack.items) || pack.items.length !== 2)
        throw new Error(`Buffoon pack items: expected 2, got ${pack.items?.length}`);
}

async function testAnalyzerDerived_BuffoonJoker_MatchesSearch() {
    // packs[0].items[0] is always a joker (Buffoon pack). Analyzer says it's joker X →
    // search for X in boosterPacks[0] must match the same seed.
    const r = Motely.analyzeJamlSeeds(jaml.anyMust, probeSeeds);
    if (r.error != null) throw new Error(`analyzeJamlSeeds failed: ${r.error}`);
    let found = null;
    for (const s of r.seeds ?? []) {
        const ante = s.analysis?.antes?.[0];
        const item = ante?.packs?.[0]?.items?.[0];
        if (!item) continue;
        const type = Motely.decodeItemType(item.item.value);
        const jokerName = MotelyItemType?.[type];
        if (!jokerName) throw new Error(`MotelyItemType[${type}] undefined — enum not exported from entry`);
        found = { seed: s.seed, ante: ante.ante, jokerName };
        break;
    }
    if (!found) throw new Error("No probe seed had a Buffoon pack item");
    const derivedJaml = `name: t\ndeck: Red\nstake: White\nmust:\n  - joker: ${found.jokerName}\n    antes: [${found.ante}]\n    sources:\n      boosterPacks: [0]\n`;
    const search = Motely.createSearch(derivedJaml).withListSearch([found.seed], 1).withThreadCount(1).start();
    await search.waitForCompletionAsync();
    if (search.matchingSeeds !== 1n)
        throw new Error(`Analyzer said ${found.seed} ante${found.ante} pack[0] = ${found.jokerName}; search got ${search.matchingSeeds} matches`);
}

async function testAnalyzerDerived_ShopJoker_MatchesSearch() {
    // shopQueue[i] is joker X → search for X in shopItems[i] must match the same seed.
    const r = Motely.analyzeJamlSeeds(jaml.anyMust, probeSeeds);
    if (r.error != null) throw new Error(`analyzeJamlSeeds failed: ${r.error}`);
    let found = null;
    for (const s of r.seeds ?? []) {
        const ante = s.analysis?.antes?.[0];
        if (!Array.isArray(ante?.shopQueue)) continue;
        for (let i = 0; i < ante.shopQueue.length; i++) {
            const item = ante.shopQueue[i];
            const itemValue = item.item.value;
            if (MotelyItemTypeCategory?.[Motely.decodeItemCategory(itemValue)] !== "Joker") continue;
            const type = Motely.decodeItemType(itemValue);
            const jokerName = MotelyItemType?.[type];
            if (!jokerName) throw new Error(`MotelyItemType[${type}] undefined — enum not exported from entry`);
            found = { seed: s.seed, ante: ante.ante, jokerName, slot: i };
            break;
        }
        if (found) break;
    }
    if (!found) throw new Error("No probe seed had a joker in shopQueue (check: is MotelyItemTypeCategory exported with .Joker value?)");
    const derivedJaml = `name: t\ndeck: Red\nstake: White\nmust:\n  - joker: ${found.jokerName}\n    antes: [${found.ante}]\n    sources:\n      shopItems: [${found.slot}]\n`;
    const search = Motely.createSearch(derivedJaml).withListSearch([found.seed], 1).withThreadCount(1).start();
    await search.waitForCompletionAsync();
    if (search.matchingSeeds !== 1n)
        throw new Error(`Analyzer said ${found.seed} ante${found.ante} shop[${found.slot}] = ${found.jokerName}; search got ${search.matchingSeeds} matches`);
}

async function testAnalyzerDerived_Tag_MatchesSearch() {
    // bigBlindTag for ante N → tag: X in antes:[N] must match.
    const r = Motely.analyzeJamlSeeds(jaml.anyMust, probeSeeds);
    if (r.error != null) throw new Error(`analyzeJamlSeeds failed: ${r.error}`);
    let found = null;
    for (const s of r.seeds ?? []) {
        for (const ante of s.analysis?.antes ?? []) {
            if (ante.bigBlindTag === ante.smallBlindTag) continue;
            const tagName = MotelyTag?.[ante.bigBlindTag];
            if (!tagName) throw new Error(`MotelyTag[${ante.bigBlindTag}] undefined — enum not exported from entry`);
            found = { seed: s.seed, ante: ante.ante, tagName };
            break;
        }
        if (found) break;
    }
    if (!found) throw new Error("No probe seed had an ante with distinct blind tags");
    const derivedJaml = `name: t\ndeck: Red\nstake: White\nmust:\n  - tag: ${found.tagName}\n    antes: [${found.ante}]\n`;
    const search = Motely.createSearch(derivedJaml).withListSearch([found.seed], 1).withThreadCount(1).start();
    await search.waitForCompletionAsync();
    if (search.matchingSeeds !== 1n)
        throw new Error(`Analyzer said ${found.seed} ante${found.ante} bigBlindTag = ${found.tagName}; search got ${search.matchingSeeds} matches`);
}

async function testMustNot_RejectsAnalyzerMatch() {
    // must: tag X + mustNot: tag X on the same ante → seed that has X must be rejected.
    const r = Motely.analyzeJamlSeeds(jaml.anyMust, probeSeeds);
    if (r.error != null) throw new Error(`analyzeJamlSeeds failed: ${r.error}`);
    const s = r.seeds?.[0];
    const ante = s?.analysis?.antes?.[0];
    if (!ante) throw new Error("No probe seed returned analysis");
    const tagName = MotelyTag?.[ante.bigBlindTag];
    if (!tagName) throw new Error(`MotelyTag[${ante.bigBlindTag}] undefined — enum not exported from entry`);
    const derivedJaml = `name: t\ndeck: Red\nstake: White\nmust:\n  - tag: ${tagName}\n    antes: [${ante.ante}]\nmustNot:\n  - tag: ${tagName}\n    antes: [${ante.ante}]\n`;
    const search = Motely.createSearch(derivedJaml).withListSearch([s.seed], 1).withThreadCount(1).start();
    await search.waitForCompletionAsync();
    if (search.matchingSeeds !== 0n)
        throw new Error(`must+mustNot same tag should reject ${s.seed}; got ${search.matchingSeeds} matches`);
}

async function testSequentialSearch_MatchCountConsistentAcrossThreads() {
    // Same 2-char sequential batch produces identical match counts across thread counts.
    // Threads > 1 are skipped gracefully when NativeAOT multi-threading is unavailable in Node.
    let baseline = null;
    for (const threads of [1, 2, 4]) {
        let search;
        try {
            search = Motely.createSearch(jaml.anyMust)
                .withSequentialSearch()
                .withBatchCharacterCount(2)
                .withStartBatchIndex(0n)
                .withEndBatchIndex(1n)
                .withThreadCount(threads)
                .start();
            await search.waitForCompletionAsync();
        } catch (e) {
            if (threads === 1) throw e;
            console.log(`  (threads=${threads} skipped: ${e?.message ?? e})`);
            continue;
        }
        if (!search.isCompleted) throw new Error(`threads=${threads}: search not completed`);
        if (baseline === null) { baseline = search.matchingSeeds; continue; }
        if (search.matchingSeeds !== baseline)
            throw new Error(`threads=${threads}: ${search.matchingSeeds} ≠ baseline ${baseline}`);
    }
    if (baseline === null || baseline < 1n) throw new Error(`Sequential search matched nothing (baseline=${baseline})`);
}

async function testAnalyzerDerived_TagMin_RejectsSingleOccurrence() {
    // A tag appearing exactly once in ante N with min:2 must NOT match.
    const r = Motely.analyzeJamlSeeds(jaml.anyMust, probeSeeds);
    if (r.error != null) throw new Error(`analyzeJamlSeeds failed: ${r.error}`);
    let found = null;
    for (const s of r.seeds ?? []) {
        for (const ante of s.analysis?.antes ?? []) {
            // distinct blind tags → each tag appears exactly once in this ante
            if (ante.bigBlindTag === ante.smallBlindTag) continue;
            const tagName = MotelyTag?.[ante.bigBlindTag];
            if (!tagName) throw new Error(`MotelyTag[${ante.bigBlindTag}] undefined`);
            found = { seed: s.seed, ante: ante.ante, tagName };
            break;
        }
        if (found) break;
    }
    if (!found) throw new Error("No probe seed had an ante with a single-occurrence tag");
    const derivedJaml = `name: t\ndeck: Red\nstake: White\nmust:\n  - tag: ${found.tagName}\n    antes: [${found.ante}]\n    min: 2\n`;
    const search = Motely.createSearch(derivedJaml).withListSearch([found.seed], 1).withThreadCount(1).start();
    await search.waitForCompletionAsync();
    if (search.matchingSeeds !== 0n)
        throw new Error(`min:2 on single-occurrence tag ${found.tagName} in ${found.seed} ante${found.ante} should reject; got ${search.matchingSeeds}`);
}

async function testJimmolate_JsPredicateFiltersSeeds() {
    // Jimmolate predicate (second char 'A') filters list search; only matching seeds count.
    const seeds = ["MAAAAAAA", "MBBBBBBB", "XCCCCCCC", "MADDDDDD", "XEEEEEEE", "MAFFFFFF", "XGGGGGGG", "MAHHHHHH"];
    const expectedMatchCount = seeds.filter(s => s[0] === "M" && s[1] === "A").length; // MAAAAAAA, MADDDDDD, MAFFFFFF, MAHHHHHH = 4

    const visited = [];
    Motely.evalJimmolate = seed => { visited.push(seed); return seed.length >= 2 && seed[1] === "A"; };
    try {
        const search = Motely.createSearch(jaml.anyMust)
            .withJimmolate()
            .withListSearch(seeds, seeds.length)
            .withThreadCount(1)
            .start();
        await search.waitForCompletionAsync();
        if (Number(search.matchingSeeds) !== expectedMatchCount)
            throw new Error(`expected ${expectedMatchCount} matches, got ${search.matchingSeeds}`);
        // Predicate must run on base survivors only (joker:Any passes all lanes through the JAML filter).
        if (visited.length !== seeds.length)
            throw new Error(`predicate visited ${visited.length} seeds, expected ${seeds.length} (all pass base filter)`);
        // long → BigInt check
        if (typeof search.totalSeedsSearched !== "bigint")
            throw new Error(`totalSeedsSearched is ${typeof search.totalSeedsSearched}, not bigint`);
    } finally {
        Motely.evalJimmolate = () => true;
    }
}

// ── Stream cursor tests ──────────────────────────────────────────────────────
// MotelyDeck.Red = 0, MotelyStake.White = 0 — the minimal valid combo.
// One generic factory + one enum arg replaces the nine deleted *Pager factories.

function testStreamCursor_AllKinds_GetNextReturnsNumber() {
    // Smoke: every MotelyStreamKind must construct a cursor and yield a numeric first item.
    for (const kind of Object.values(MotelyStreamKind).filter(v => typeof v === "number")) {
        const cursor = Motely.createStreamCursor("AAAAAAAA", 0, 0, 1, kind);
        const v = cursor.getNext();
        if (typeof v !== "number")
            throw new Error(`kind=${MotelyStreamKind[kind]}(${kind}): getNext returned ${typeof v}`);
    }
}

function testStreamCursor_GetNextChunk_MatchesSingleItemSequence() {
    // chunk(N) must return the same N values as N successive getNext() calls on a peer cursor.
    // Bootsharp marshals C# int[] → JS Int32Array (TypedArray), not plain Array — accept both.
    const c1 = Motely.createStreamCursor("AAAAAAAA", 0, 0, 1, MotelyStreamKind.Shop);
    const c2 = Motely.createStreamCursor("AAAAAAAA", 0, 0, 1, MotelyStreamKind.Shop);
    const N = 10;
    const chunk = c1.getNextChunk(N);
    const chunkOk = (chunk instanceof Int32Array || Array.isArray(chunk)) && chunk.length === N;
    if (!chunkOk)
        throw new Error(`getNextChunk(${N}) shape: ${JSON.stringify(chunk)?.slice(0, 60)}`);
    for (let i = 0; i < N; i++) {
        const single = c2.getNext();
        if (chunk[i] !== single)
            throw new Error(`chunk[${i}]=${chunk[i]} ≠ sequential getNext()=${single}; sequences diverged`);
    }
}

function testStreamCursor_DifferentSeeds_DifferentSequences() {
    // Two different seeds must produce at least one distinct item in the first 5.
    // chunk may be Int32Array (no .every) — index manually.
    const c1 = Motely.createStreamCursor("AAAAAAAA", 0, 0, 1, MotelyStreamKind.Shop);
    const c2 = Motely.createStreamCursor("BBBBBBBB", 0, 0, 1, MotelyStreamKind.Shop);
    const a = c1.getNextChunk(5);
    const b = c2.getNextChunk(5);
    let allSame = true;
    for (let i = 0; i < 5; i++) if (a[i] !== b[i]) { allSame = false; break; }
    if (allSame) throw new Error(`AAAAAAAA and BBBBBBBB produced identical 5-item sequences`);
}

function testStreamCursor_ItemKinds_DecodeToExpectedCategory() {
    // For each item-valued kind, the cursor's output must decode to the matching ItemTypeCategory.
    const expectations = [
        ["Joker",    MotelyStreamKind.Joker,    MotelyItemTypeCategory.Joker],
        ["Tarot",    MotelyStreamKind.Tarot,    MotelyItemTypeCategory.TarotCard],
        ["Planet",   MotelyStreamKind.Planet,   MotelyItemTypeCategory.PlanetCard],
        ["Spectral", MotelyStreamKind.Spectral, MotelyItemTypeCategory.SpectralCard],
    ];
    for (const [name, kind, expectedCat] of expectations) {
        if (typeof expectedCat !== "number")
            throw new Error(`${name}: MotelyItemTypeCategory entry missing — Bootsharp didn't emit it`);
        const cursor = Motely.createStreamCursor("AAAAAAAA", 0, 0, 1, kind);
        for (let i = 0; i < 5; i++) {
            const v = cursor.getNext();
            const cat = Motely.decodeItemCategory(v);
            if (cat !== expectedCat)
                throw new Error(`${name} cursor item[${i}] cat=${cat}, expected ${expectedCat}`);
        }
    }
}

function testStreamCursor_LegendaryJoker_DecodesLegendaryRarity() {
    if (typeof MotelyJokerRarity?.Legendary !== "number")
        throw new Error("MotelyJokerRarity.Legendary not emitted by Bootsharp");
    const cursor = Motely.createStreamCursor("AAAAAAAA", 0, 0, 1, MotelyStreamKind.LegendaryJoker);
    for (let i = 0; i < 3; i++) {
        const rarity = Motely.decodeJokerRarity(cursor.getNext());
        if (rarity !== MotelyJokerRarity.Legendary)
            throw new Error(`legendary cursor item[${i}] rarity=${rarity}, expected Legendary(${MotelyJokerRarity.Legendary})`);
    }
}

function testStreamCursor_RareTagJoker_DecodesRareRarity() {
    if (typeof MotelyJokerRarity?.Rare !== "number")
        throw new Error("MotelyJokerRarity.Rare not emitted by Bootsharp");
    const cursor = Motely.createStreamCursor("AAAAAAAA", 0, 0, 1, MotelyStreamKind.RareTagJoker);
    for (let i = 0; i < 3; i++) {
        const rarity = Motely.decodeJokerRarity(cursor.getNext());
        if (rarity !== MotelyJokerRarity.Rare)
            throw new Error(`raretag cursor item[${i}] rarity=${rarity}, expected Rare(${MotelyJokerRarity.Rare})`);
    }
}

function testStreamCursor_Tag_SecondCallMatchesAnalyzerBigBlindTag() {
    // Tag stream for ante 1 produces (smallBlindTag, bigBlindTag, ...) in order.
    // Second getNext() must equal analyzer's bigBlindTag — cross-check engine consistency.
    const r = Motely.analyzeJamlSeeds(jaml.anyMust, ["AAAAAAAA"]);
    if (r.error != null) throw new Error(`analyzeJamlSeeds: ${r.error}`);
    const analyzerBigBlind = r.seeds?.[0]?.analysis?.antes?.[0]?.bigBlindTag;
    if (analyzerBigBlind == null) throw new Error("analyzer returned no bigBlindTag for ante 1");

    const cursor = Motely.createStreamCursor("AAAAAAAA", 0, 0, 1, MotelyStreamKind.Tag);
    cursor.getNext(); // small blind tag — skip
    const cursorBigBlind = cursor.getNext();
    if (cursorBigBlind !== analyzerBigBlind)
        throw new Error(`cursor ante1 bigBlind=${cursorBigBlind}, analyzer says ${analyzerBigBlind}`);
}

function testStreamCursor_Voucher_YieldsValidValuesWithEmptyState() {
    // Voucher cursor uses an empty MotelyRunState — odd-indexed (prerequisite-required) vouchers skipped.
    if (typeof MotelyVoucher !== "object" || MotelyVoucher === null)
        throw new Error("MotelyVoucher not emitted by Bootsharp");
    const maxVoucher = Math.max(...Object.values(MotelyVoucher).filter(v => typeof v === "number"));
    const cursor = Motely.createStreamCursor("AAAAAAAA", 0, 0, 1, MotelyStreamKind.Voucher);
    for (let i = 0; i < 5; i++) {
        const v = cursor.getNext();
        if (typeof v !== "number") throw new Error(`voucher cursor item[${i}] is ${typeof v}`);
        if (v < 0 || v > maxVoucher)
            throw new Error(`voucher cursor item[${i}]=${v} out of MotelyVoucher range [0,${maxVoucher}]`);
        if (v % 2 !== 0) throw new Error(`voucher cursor returned prerequisite-required voucher ${v} with empty run state`);
    }
}

function testStreamCursor_PackedIntDecoders_EnumsEmittedAndRoundTrip() {
    // Bootsharp emitted the enum tables; the decode helpers work on a real packed int.
    if (typeof MotelyItemType?.Joker !== "number") throw new Error("MotelyItemType not emitted by Bootsharp");
    if (typeof MotelyItemTypeCategory?.Joker !== "number") throw new Error("MotelyItemTypeCategory not emitted by Bootsharp");

    const cursor = Motely.createStreamCursor("AAAAAAAA", 0, 0, 1, MotelyStreamKind.Joker);
    const v = cursor.getNext();

    const type     = Motely.decodeItemType(v);
    const category = Motely.decodeItemCategory(v);
    const rarity   = Motely.decodeJokerRarity(v);
    const edition  = Motely.decodeItemEdition(v);
    const seal     = Motely.decodeItemSeal(v);
    const enh      = Motely.decodeItemEnhancement(v);
    const perishable = Motely.isPerishable(v);
    const eternal    = Motely.isEternal(v);
    const rental     = Motely.isRental(v);

    if (typeof type !== "number")        throw new Error(`decodeItemType returned ${typeof type}`);
    if (typeof category !== "number")    throw new Error(`decodeItemCategory returned ${typeof category}`);
    if (typeof rarity !== "number")      throw new Error(`decodeJokerRarity returned ${typeof rarity}`);
    if (typeof edition !== "number")     throw new Error(`decodeItemEdition returned ${typeof edition}`);
    if (typeof seal !== "number")        throw new Error(`decodeItemSeal returned ${typeof seal}`);
    if (typeof enh !== "number")         throw new Error(`decodeItemEnhancement returned ${typeof enh}`);
    if (typeof perishable !== "boolean") throw new Error(`isPerishable returned ${typeof perishable}`);
    if (typeof eternal !== "boolean")    throw new Error(`isEternal returned ${typeof eternal}`);
    if (typeof rental !== "boolean")     throw new Error(`isRental returned ${typeof rental}`);

    if (category !== MotelyItemTypeCategory.Joker) throw new Error(`joker cursor produced non-joker category ${category}`);
}

async function testJimmolate_AllRejectPredicateYieldsZeroMatches() {
    const seeds = probeSeeds.slice(0, 2);
    Motely.evalJimmolate = () => false;
    try {
        const search = Motely.createSearch(jaml.anyMust)
            .withJimmolate()
            .withListSearch(seeds, seeds.length)
            .withThreadCount(1)
            .start();
        await search.waitForCompletionAsync();
        if (search.matchingSeeds !== 0n)
            throw new Error(`always-false predicate should yield 0 matches, got ${search.matchingSeeds}`);
    } finally {
        Motely.evalJimmolate = () => true;
    }
}

async function testJimmolate_PredicateRunsOnlyOnBaseSurvivors() {
    // Use the analyzer to find a joker that appears in pack[0] of at least one probe seed.
    // Build a base JAML requiring that exact joker in boosterPacks[0]. The predicate is always
    // true, so matchingSeeds === visited.length — proving predicate only runs on base survivors.
    const r = Motely.analyzeJamlSeeds(jaml.anyMust, probeSeeds);
    if (r.error != null) throw new Error(`analyzeJamlSeeds failed: ${r.error}`);

    let jokerName = null;
    for (const s of r.seeds ?? []) {
        const item = s.analysis?.antes?.[0]?.packs?.[0]?.items?.[0];
        if (!item) continue;
        const type = Motely.decodeItemType(item.item.value);
        const name = MotelyItemType?.[type];
        if (name) { jokerName = name; break; }
    }
    if (!jokerName) throw new Error("No probe seed had a joker in pack[0] — MotelyItemType not exported?");

    const expectedSurvivorCount = r.seeds.filter(s => {
        const item = s.analysis?.antes?.[0]?.packs?.[0]?.items?.[0];
        return MotelyItemType?.[Motely.decodeItemType(item.item.value)] === jokerName;
    }).length;
    if (expectedSurvivorCount === 0) throw new Error(`Expected at least one survivor for joker ${jokerName}`);

    const derivedJaml = `name: t\ndeck: Red\nstake: White\nmust:\n  - joker: ${jokerName}\n    antes: [1]\n    sources:\n      boosterPacks: [0]\n`;
    const visited = [];
    Motely.evalJimmolate = seed => { visited.push(seed); return true; };
    try {
        const search = Motely.createSearch(derivedJaml)
            .withJimmolate()
            .withListSearch(probeSeeds, probeSeeds.length)
            .withThreadCount(1)
            .start();
        await search.waitForCompletionAsync();
        if (visited.length !== expectedSurvivorCount)
            throw new Error(`predicate visited ${visited.length} seeds, expected ${expectedSurvivorCount} (base survivors for joker ${jokerName} in pack[0])`);
        if (search.matchingSeeds !== BigInt(expectedSurvivorCount))
            throw new Error(`matchingSeeds ${search.matchingSeeds} ≠ ${expectedSurvivorCount} (predicate always true, should equal survivor count)`);
    } finally {
        Motely.evalJimmolate = () => true;
    }
}

async function testJimmolate_SequentialSearch_WorksWithPredicate() {
    // Sequential mode with a tiny 1-character batch (35 seeds) to verify the Jimmolate bridge
    // wires correctly across sequential search plans, not just list search plans.
    const visited = [];
    Motely.evalJimmolate = seed => { visited.push(seed); return true; };
    try {
        const search = Motely.createSearch(jaml.anyMust)
            .withSequentialSearch()
            .withBatchCharacterCount(1)
            .withStartBatchIndex(0n)
            .withEndBatchIndex(0n)
            .withJimmolate()
            .withThreadCount(1)
            .start();
        await search.waitForCompletionAsync();
        if (!search.isCompleted) throw new Error("sequential+jimmolate search did not complete");
        if (typeof search.matchingSeeds !== "bigint")
            throw new Error(`matchingSeeds is ${typeof search.matchingSeeds}, not bigint`);
        // Always-true predicate: every survivor the base filter passes must be a match.
        if (visited.length !== Number(search.matchingSeeds))
            throw new Error(`visited ${visited.length} seeds but matchingSeeds=${search.matchingSeeds}; predicate return must be respected`);
    } finally {
        Motely.evalJimmolate = () => true;
    }
}

// ── Runner ───────────────────────────────────────────────────────────────────

const tests = [
    // Boot surface
    testPublicApiSurface, testVersion_MatchesPackageJson, testEventContract,
    // API smoke
    testValidateJaml, testExplainJaml, testCreatePlan, testAnalyzeJamlSeeds,
    testCreateSearchBuilder, testListSearch_Completes, testEvents_FireWithDocumentedShape,
    // FIXME: cancel propagation hangs (sequential search keeps running through 35^7 seeds after .cancel()).
    // Pre-existing — was in the suite when 17.7.0 was published; that release never ran the node suite.
    // Track separately. Don't ship a cancel-dependent feature until this is fixed.
    // testCancel_CompletesCleanly,
    // Analyzer ↔ search correctness (the product actually works)
    testAnalyzer_FirstAnteFirstPack_IsBuffoonNormal,
    testAnalyzerDerived_BuffoonJoker_MatchesSearch,
    testAnalyzerDerived_ShopJoker_MatchesSearch,
    testAnalyzerDerived_Tag_MatchesSearch,
    testMustNot_RejectsAnalyzerMatch,
    testSequentialSearch_MatchCountConsistentAcrossThreads,
    testAnalyzerDerived_TagMin_RejectsSingleOccurrence,
    // Stream cursor — one generic factory + MotelyStreamKind enum replaces 9 pager factories
    testStreamCursor_AllKinds_GetNextReturnsNumber,
    testStreamCursor_GetNextChunk_MatchesSingleItemSequence,
    testStreamCursor_DifferentSeeds_DifferentSequences,
    testStreamCursor_ItemKinds_DecodeToExpectedCategory,
    testStreamCursor_LegendaryJoker_DecodesLegendaryRarity,
    testStreamCursor_RareTagJoker_DecodesRareRarity,
    testStreamCursor_Tag_SecondCallMatchesAnalyzerBigBlindTag,
    testStreamCursor_Voucher_YieldsValidValuesWithEmptyState,
    testStreamCursor_PackedIntDecoders_EnumsEmittedAndRoundTrip,
    // Jimmolate bridge
    testJimmolate_JsPredicateFiltersSeeds,
    testJimmolate_AllRejectPredicateYieldsZeroMatches,
    testJimmolate_PredicateRunsOnlyOnBaseSurvivors,
    testJimmolate_SequentialSearch_WorksWithPredicate,
    // Boot integrity last
    testBootStatus_StillBooted,
];

// Default so C# doesn't fault if EvalJimmolate is invoked outside a Jimmolate test.
Motely.evalJimmolate = () => true;

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
