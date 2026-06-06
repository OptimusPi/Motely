import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { jaml, probeSeeds } from "./fixtures.mjs";

const {
    Motely,
    MotelyBoosterPack,
    MotelyItemType,
    MotelyItemTypeCategory,
    MotelyTag,
} = harness;

function analyze(cfg, seeds) {
    cfg.seeds = seeds;
    return Motely.jamlyzer(cfg);
}

function runListSearch(jamlText, seeds) {
    const cfg = Motely.parseJaml(jamlText);
    cfg.seeds = seeds;
    return Motely.runSeedListSearch(cfg);
}

describe("analyzer", () => {
    it("ante 1 pack 0 is a 2-item Buffoon pack", () => {
        const r = analyze(Motely.parseJaml(jaml.anyMust), probeSeeds);
        assert.ok(r.error == null);
        const pack = r.seeds?.[0]?.analysis?.antes?.[0]?.packs?.[0];
        assert.ok(pack);
        const packName = MotelyBoosterPack?.[pack.type];
        assert.equal(packName, "Buffoon");
        assert.equal(pack.items?.length, 2);
    });

    it("Buffoon pack joker matches list search on same seed", () => {
        const r = analyze(Motely.parseJaml(jaml.anyMust), probeSeeds);
        assert.ok(r.error == null);

        let found = null;
        for (const s of r.seeds ?? []) {
            const ante = s.analysis?.antes?.[0];
            const item = ante?.packs?.[0]?.items?.[0];
            if (!item) continue;
            const jokerName = MotelyItemType?.[item.item.type];
            if (!jokerName) continue;
            found = { seed: s.seed, ante: ante.ante, jokerName };
            break;
        }
        assert.ok(found, "expected to find a known Buffoon pack joker in probe seeds");

        const derivedJaml = `name: t\ndeck: Red\nstake: White\nmust:\n  - joker: ${found.jokerName}\n    antes: [${found.ante}]\n    sources:\n      boosterPacks: [0]\n`;
        const res = runListSearch(derivedJaml, [found.seed]);
        assert.equal(res.matchingSeeds, 1n);
    });

    it("shop joker matches list search on same seed", () => {
        const r = analyze(Motely.parseJaml(jaml.anyMust), probeSeeds);
        assert.ok(r.error == null);

        let found = null;
        for (const s of r.seeds ?? []) {
            const ante = s.analysis?.antes?.[0];
            if (!Array.isArray(ante?.shopQueue)) continue;
            for (let i = 0; i < ante.shopQueue.length; i++) {
                const item = ante.shopQueue[i];
                if (MotelyItemTypeCategory?.[item.item.typeCategory] !== "Joker") continue;
                const jokerName = MotelyItemType?.[item.item.type];
                if (!jokerName) continue;
                found = { seed: s.seed, ante: ante.ante, jokerName, slot: i };
                break;
            }
            if (found) break;
        }
        assert.ok(found, "expected to find a known shop joker in probe seeds");

        const derivedJaml = `name: t\ndeck: Red\nstake: White\nmust:\n  - joker: ${found.jokerName}\n    antes: [${found.ante}]\n    sources:\n      shopItems: [${found.slot}]\n`;
        const res = runListSearch(derivedJaml, [found.seed]);
        assert.equal(res.matchingSeeds, 1n);
    });

    it("bigBlindTag matches tag search on same seed", () => {
        const r = analyze(Motely.parseJaml(jaml.anyMust), probeSeeds);
        assert.ok(r.error == null);

        let found = null;
        for (const s of r.seeds ?? []) {
            for (const ante of s.analysis?.antes ?? []) {
                if (ante.bigBlindTag === ante.smallBlindTag) continue;
                const tagName = MotelyTag?.[ante.bigBlindTag];
                assert.ok(tagName);
                found = { seed: s.seed, ante: ante.ante, tagName };
                break;
            }
            if (found) break;
        }
        assert.ok(found);

        const derivedJaml = `name: t\ndeck: Red\nstake: White\nmust:\n  - tag: ${found.tagName}\n    antes: [${found.ante}]\n`;
        const res = runListSearch(derivedJaml, [found.seed]);
        assert.equal(res.matchingSeeds, 1n);
    });

    it("must + mustNot same tag rejects seed", () => {
        const r = analyze(Motely.parseJaml(jaml.anyMust), probeSeeds);
        assert.ok(r.error == null);
        const s = r.seeds?.[0];
        const ante = s?.analysis?.antes?.[0];
        assert.ok(ante);
        const tagName = MotelyTag?.[ante.bigBlindTag];
        assert.ok(tagName);

        const derivedJaml = `name: t\ndeck: Red\nstake: White\nmust:\n  - tag: ${tagName}\n    antes: [${ante.ante}]\nmustNot:\n  - tag: ${tagName}\n    antes: [${ante.ante}]\n`;
        const res = runListSearch(derivedJaml, [s.seed]);
        assert.equal(res.matchingSeeds, 0n);
    });

    it("tag min:2 rejects single occurrence", () => {
        const r = analyze(Motely.parseJaml(jaml.anyMust), probeSeeds);
        assert.ok(r.error == null);

        let found = null;
        for (const s of r.seeds ?? []) {
            for (const ante of s.analysis?.antes ?? []) {
                if (ante.bigBlindTag === ante.smallBlindTag) continue;
                const tagName = MotelyTag?.[ante.bigBlindTag];
                assert.ok(tagName);
                found = { seed: s.seed, ante: ante.ante, tagName };
                break;
            }
            if (found) break;
        }
        assert.ok(found);

        const derivedJaml = `name: t\ndeck: Red\nstake: White\nmust:\n  - tag: ${found.tagName}\n    antes: [${found.ante}]\n    min: 2\n`;
        const res = runListSearch(derivedJaml, [found.seed]);
        assert.equal(res.matchingSeeds, 0n);
    });
});
