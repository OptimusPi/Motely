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

describe("analyzer", () => {
    it("ante 1 pack 0 is a 2-item Buffoon pack", () => {
        const r = Motely.analyzeJamlSeeds(jaml.anyMust, probeSeeds);
        assert.ok(r.error == null);
        const pack = r.seeds?.[0]?.analysis?.antes?.[0]?.packs?.[0];
        assert.ok(pack);
        const packName = MotelyBoosterPack?.[pack.type];
        assert.equal(packName, "Buffoon");
        assert.equal(pack.items?.length, 2);
    });

    it("Buffoon pack joker matches list search on same seed", async () => {
        const r = Motely.analyzeJamlSeeds(jaml.anyMust, probeSeeds);
        assert.ok(r.error == null);

        let found = null;
        for (const s of r.seeds ?? []) {
            const ante = s.analysis?.antes?.[0];
            const item = ante?.packs?.[0]?.items?.[0];
            if (!item) continue;
            const type = Motely.decodeItemType(item.item.value);
            const jokerName = MotelyItemType?.[type];
            assert.ok(jokerName, `MotelyItemType[${type}]`);
            found = { seed: s.seed, ante: ante.ante, jokerName };
            break;
        }
        assert.ok(found);

        const derivedJaml = `name: t\ndeck: Red\nstake: White\nmust:\n  - joker: ${found.jokerName}\n    antes: [${found.ante}]\n    sources:\n      boosterPacks: [0]\n`;
        const search = Motely.createSearch(derivedJaml)
            .withListSearch([found.seed], 1)
            .withThreadCount(1)
            .start();
        await search.waitForCompletionAsync();
        assert.equal(search.matchingSeeds, 1n);
    });

    it("shop joker matches list search on same seed", async () => {
        const r = Motely.analyzeJamlSeeds(jaml.anyMust, probeSeeds);
        assert.ok(r.error == null);

        let found = null;
        for (const s of r.seeds ?? []) {
            const ante = s.analysis?.antes?.[0];
            if (!Array.isArray(ante?.shopQueue)) continue;
            for (let i = 0; i < ante.shopQueue.length; i++) {
                const item = ante.shopQueue[i];
                const itemValue = item.item.value;
                if (
                    MotelyItemTypeCategory?.[Motely.decodeItemCategory(itemValue)] !==
                    "Joker"
                ) {
                    continue;
                }
                const type = Motely.decodeItemType(itemValue);
                const jokerName = MotelyItemType?.[type];
                assert.ok(jokerName);
                found = { seed: s.seed, ante: ante.ante, jokerName, slot: i };
                break;
            }
            if (found) break;
        }
        assert.ok(found);

        const derivedJaml = `name: t\ndeck: Red\nstake: White\nmust:\n  - joker: ${found.jokerName}\n    antes: [${found.ante}]\n    sources:\n      shopItems: [${found.slot}]\n`;
        const search = Motely.createSearch(derivedJaml)
            .withListSearch([found.seed], 1)
            .withThreadCount(1)
            .start();
        await search.waitForCompletionAsync();
        assert.equal(search.matchingSeeds, 1n);
    });

    it("bigBlindTag matches tag search on same seed", async () => {
        const r = Motely.analyzeJamlSeeds(jaml.anyMust, probeSeeds);
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
        const search = Motely.createSearch(derivedJaml)
            .withListSearch([found.seed], 1)
            .withThreadCount(1)
            .start();
        await search.waitForCompletionAsync();
        assert.equal(search.matchingSeeds, 1n);
    });

    it("must + mustNot same tag rejects seed", async () => {
        const r = Motely.analyzeJamlSeeds(jaml.anyMust, probeSeeds);
        assert.ok(r.error == null);
        const s = r.seeds?.[0];
        const ante = s?.analysis?.antes?.[0];
        assert.ok(ante);
        const tagName = MotelyTag?.[ante.bigBlindTag];
        assert.ok(tagName);

        const derivedJaml = `name: t\ndeck: Red\nstake: White\nmust:\n  - tag: ${tagName}\n    antes: [${ante.ante}]\nmustNot:\n  - tag: ${tagName}\n    antes: [${ante.ante}]\n`;
        const search = Motely.createSearch(derivedJaml)
            .withListSearch([s.seed], 1)
            .withThreadCount(1)
            .start();
        await search.waitForCompletionAsync();
        assert.equal(search.matchingSeeds, 0n);
    });

    it("tag min:2 rejects single occurrence", async () => {
        const r = Motely.analyzeJamlSeeds(jaml.anyMust, probeSeeds);
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
        const search = Motely.createSearch(derivedJaml)
            .withListSearch([found.seed], 1)
            .withThreadCount(1)
            .start();
        await search.waitForCompletionAsync();
        assert.equal(search.matchingSeeds, 0n);
    });
});
