import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { jaml } from "./fixtures.mjs";

const { Motely, pkgVersion } = harness;

describe("public API surface", () => {
    it("exports documented Motely methods", () => {
        const required = [
            "version",
            "validateJaml",
            "explainJaml",
            "createPlan",
            "analyzeJamlSeeds",
            "createSearchSettings",
            "createNativeSearchSettings",
            "nativeFilterNames",
            "fromJaml",
            "seed",
            "mountRoot",
            "unmountRoot",
            "pickRoot",
            "readTextFile",
            "writeTextFile",
            "onFileChanges",
            "onSeedMatch",
            "onScoredResult",
            "onProgress",
            "decodeItemType",
            "decodeItemCategory",
            "decodeJokerRarity",
            "decodeItemEdition",
            "decodeItemSeal",
            "decodeItemEnhancement",
            "isPerishable",
            "isEternal",
            "isRental",
        ];
        const missing = required.filter((n) => !(n in Motely));
        assert.equal(missing.length, 0, `Motely missing: ${missing.join(", ")}`);
    });

    it("version matches package.json", () => {
        const v = Motely.version();
        assert.match(v, /^\d+\.\d+\.\d+/);
        assert.ok(
            v.startsWith(pkgVersion),
            `assembly ${v} ≠ package.json ${pkgVersion}`
        );
    });

    it("onFileChanges is a Bootsharp EventSubscriber", () => {
        const ev = Motely.onFileChanges;
        assert.equal(typeof ev?.subscribe, "function", "onFileChanges.subscribe");
        assert.equal(typeof ev?.unsubscribe, "function", "onFileChanges.unsubscribe");
        assert.ok("last" in ev, "onFileChanges.last");
    });

    it("wasm load exposes search event subscribers on Motely", () => {
        for (const name of ["onSeedMatch", "onScoredResult", "onProgress"]) {
            const ev = Motely[name];
            assert.equal(typeof ev?.subscribe, "function", `${name}.subscribe`);
            assert.equal(typeof ev?.unsubscribe, "function", `${name}.unsubscribe`);
            assert.ok("last" in ev, `${name}.last`);
        }
    });

});

describe("JAML API", () => {
    it("validateJaml accepts valid and rejects garbage", () => {
        assert.equal(Motely.validateJaml(jaml.must), "valid");
        const err = Motely.validateJaml(jaml.invalid);
        assert.equal(typeof err, "string");
        assert.notEqual(err, "valid");
        assert.ok(err.length > 0);
    });

    it("explainJaml returns plan or # ERROR:", () => {
        const r = Motely.explainJaml(jaml.must);
        assert.ok(r.startsWith("# JAML filter eval plan"));
        assert.ok(r.includes("WeeJoker"));
        const errResult = Motely.explainJaml(jaml.invalid);
        assert.ok(errResult.startsWith("# ERROR:"));
    });

    it("JamlToJson and JsonToJaml round-trips correctly", () => {
        const roundtripJaml = `id: perkeo_observatory
deck: Ghost
stake: Gold
must:
  - joker: WeeJoker
    antes: [1]
`;
        const json = Motely.jamlToJson(roundtripJaml);
        assert.equal(typeof json, "string");
        
        const doc = JSON.parse(json);
        assert.equal(doc.id, "perkeo_observatory");
        assert.equal(doc.deck, "Ghost");
        assert.equal(doc.stake, "Gold");
        
        // Modify a value
        doc.deck = "Red";
        const modifiedJaml = Motely.jsonToJaml(JSON.stringify(doc));
        assert.ok(modifiedJaml.includes("deck: Red"));
    });

    it("createPlan exposes scoring structure", () => {
        const plan = Motely.createPlan(jaml.scoring);
        assert.equal(typeof plan?.scoredCsvHeaderQuoted, "string");
        assert.equal(plan.scoreTallyColumnCount, 2);
        assert.equal(plan.tallyLabels?.length, 2);
    });

    it("analyzeJamlSeeds returns populated analysis shape", () => {
        const seeds = ["1AAAAAAA", "2BBBBBBB"];
        const result = Motely.analyzeJamlSeeds(jaml.anyMust, seeds);
        assert.ok(result.error == null);
        assert.equal(result.seeds?.length, 2);
        for (let i = 0; i < seeds.length; i++) {
            assert.equal(result.seeds[i].seed, seeds[i]);
        }
        const ante = result.seeds[0].analysis?.antes?.[0];
        assert.ok(ante && "boss" in ante);
        assert.ok(Array.isArray(ante.shopQueue));
        assert.ok(Array.isArray(ante.packs));
        assert.equal(result.deck, 0);
        assert.equal(result.stake, 0);
    });

    it("fromJaml throws on garbage; builder chains", () => {
        assert.throws(() => Motely.fromJaml(jaml.invalid));
        assert.equal(
            typeof Motely.createSearchSettings()?.withSequentialSearch,
            "function"
        );
        const s = Motely.fromJaml(jaml.scoring)
            .withSequentialSearch()
            .withThreadCount(1)
            .withProgressReportIntervalMs(0n);
        assert.equal(typeof s?.start, "function");
    });

    it("createNativeSearchSettings accepts CLI native filter names", () => {
        const names = Motely.nativeFilterNames();
        assert.ok(names.length > 0);
        assert.ok(names.includes("Observatory"));
        const settings = Motely.createNativeSearchSettings("Observatory");
        assert.equal(typeof settings?.withListSearch, "function");
    });
});
