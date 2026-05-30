import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { jaml } from "./fixtures.mjs";

const { Motely } = harness;

describe("public API surface", () => {
    it("exports documented Motely methods", () => {
        const required = [
            "parseJaml",
            "explainJaml",
            "createPlan",
            "jamlyzer",
            "jamlToJson",
            "jsonToJaml",
            "nativeFilterNames",
            "runSequentialSearch",
            "runRandomSearch",
            "runSeedListSearch",
            "runAestheticSearch",
            "runNativeListSearch",
            "runPassthroughListSearch",
            "mountRoot",
            "unmountRoot",
            "pickRoot",
            "readTextFile",
            "writeTextFile",
            "onFileChanges",
            "onSeedMatch",
            "onScoredResult",
            "onProgress",
        ];
        const missing = required.filter((n) => !(n in Motely));
        assert.equal(missing.length, 0, `Motely missing: ${missing.join(", ")}`);
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
    it("parseJaml returns a JamlConfig; throws on garbage", () => {
        const cfg = Motely.parseJaml(jaml.must);
        assert.equal(typeof cfg, "object");
        assert.ok(cfg !== null);
        assert.throws(() => Motely.parseJaml(jaml.invalid));
    });

    it("explainJaml returns a plan for a valid config", () => {
        const cfg = Motely.parseJaml(jaml.must);
        const r = Motely.explainJaml(cfg);
        assert.ok(r.startsWith("# JAML filter eval plan"));
        assert.ok(r.includes("WeeJoker"));
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

        doc.deck = "Red";
        const modifiedJaml = Motely.jsonToJaml(JSON.stringify(doc));
        assert.ok(modifiedJaml.includes("deck: Red"));
    });

    it("createPlan exposes scoring structure", () => {
        const cfg = Motely.parseJaml(jaml.scoring);
        const plan = Motely.createPlan(cfg);
        assert.equal(typeof plan?.scoredCsvHeaderQuoted, "string");
        assert.equal(plan.scoreTallyColumnCount, 2);
        assert.equal(plan.tallyLabels?.length, 2);
    });

    it("jamlyzer returns populated analysis shape", () => {
        const cfg = Motely.parseJaml(jaml.anyMust);
        cfg.seeds = ["1AAAAAAA", "2BBBBBBB"];
        const result = Motely.jamlyzer(cfg);
        assert.ok(result.error == null);
        assert.equal(result.seeds?.length, 2);
        for (let i = 0; i < cfg.seeds.length; i++) {
            assert.equal(result.seeds[i].seed, cfg.seeds[i]);
        }
        const ante = result.seeds[0].analysis?.antes?.[0];
        assert.ok(ante && "boss" in ante);
        assert.ok(Array.isArray(ante.shopQueue));
        assert.ok(Array.isArray(ante.packs));
        assert.equal(result.deck, 0);
        assert.equal(result.stake, 0);
    });

    it("runSeedListSearch returns MotelySearchResult", () => {
        const cfg = Motely.parseJaml(jaml.scoring);
        cfg.seeds = ["AAAAAAAA", "BBBBBBBB"];
        const r = Motely.runSeedListSearch(cfg);
        assert.equal(r.isCompleted, true);
        assert.equal(typeof r.totalSeedsSearched, "bigint");
        assert.equal(typeof r.matchingSeeds, "bigint");
        assert.equal(typeof r.elapsedMs, "bigint");
    });

    it("createNativeSearchSettings filter names exist", () => {
        const names = Motely.nativeFilterNames();
        assert.ok(names.length > 0);
        assert.ok(names.includes("Observatory"));
    });
});
