import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { jaml } from "./fixtures.mjs";

const {
    Motely,
    MotelyStreamKind,
    MotelyItemType,
    MotelyItemTypeCategory,
    MotelyJokerRarity,
    MotelyVoucher,
} = harness;

const streamCursorReady =
    typeof Motely.createStreamCursor === "function" && MotelyStreamKind != null;

describe("stream cursor", {
    skip: streamCursorReady
        ? false
        : "Motely.createStreamCursor / MotelyStreamKind not on WASM export surface yet",
}, () => {
    it("every MotelyStreamKind getNext returns number", () => {
        for (const kind of Object.values(MotelyStreamKind).filter(
            (v) => typeof v === "number"
        )) {
            const cursor = Motely.createStreamCursor("AAAAAAAA", 0, 0, 1, kind);
            const v = cursor.getNext();
            assert.equal(typeof v, "number", `kind=${MotelyStreamKind[kind]}`);
        }
    });

    it("getNextChunk matches sequential getNext", () => {
        const c1 = Motely.createStreamCursor("AAAAAAAA", 0, 0, 1, MotelyStreamKind.Shop);
        const c2 = Motely.createStreamCursor("AAAAAAAA", 0, 0, 1, MotelyStreamKind.Shop);
        const N = 10;
        const chunk = c1.getNextChunk(N);
        const chunkOk =
            (chunk instanceof Int32Array || Array.isArray(chunk)) &&
            chunk.length === N;
        assert.ok(chunkOk);
        for (let i = 0; i < N; i++) {
            assert.equal(chunk[i], c2.getNext(), `index ${i}`);
        }
    });

    it("different seeds produce different sequences", () => {
        const c1 = Motely.createStreamCursor("AAAAAAAA", 0, 0, 1, MotelyStreamKind.Shop);
        const c2 = Motely.createStreamCursor("BBBBBBBB", 0, 0, 1, MotelyStreamKind.Shop);
        const a = c1.getNextChunk(5);
        const b = c2.getNextChunk(5);
        let allSame = true;
        for (let i = 0; i < 5; i++) {
            if (a[i] !== b[i]) {
                allSame = false;
                break;
            }
        }
        assert.equal(allSame, false);
    });

    it("item streams decode to expected categories", () => {
        const expectations = [
            ["Joker", MotelyStreamKind.Joker, MotelyItemTypeCategory.Joker],
            ["Tarot", MotelyStreamKind.Tarot, MotelyItemTypeCategory.TarotCard],
            ["Planet", MotelyStreamKind.Planet, MotelyItemTypeCategory.PlanetCard],
            ["Spectral", MotelyStreamKind.Spectral, MotelyItemTypeCategory.SpectralCard],
        ];
        for (const [name, kind, expectedCat] of expectations) {
            assert.equal(typeof expectedCat, "number", `${name} category enum`);
            const cursor = Motely.createStreamCursor("AAAAAAAA", 0, 0, 1, kind);
            for (let i = 0; i < 5; i++) {
                const cat = Motely.decodeItemCategory(cursor.getNext());
                assert.equal(cat, expectedCat, `${name}[${i}]`);
            }
        }
    });

    it("legendary joker stream decodes legendary rarity", () => {
        assert.equal(typeof MotelyJokerRarity?.Legendary, "number");
        const cursor = Motely.createStreamCursor(
            "AAAAAAAA",
            0,
            0,
            1,
            MotelyStreamKind.LegendaryJoker
        );
        for (let i = 0; i < 3; i++) {
            assert.equal(
                Motely.decodeJokerRarity(cursor.getNext()),
                MotelyJokerRarity.Legendary
            );
        }
    });

    it("rare tag joker stream decodes rare rarity", () => {
        assert.equal(typeof MotelyJokerRarity?.Rare, "number");
        const cursor = Motely.createStreamCursor(
            "AAAAAAAA",
            0,
            0,
            1,
            MotelyStreamKind.RareTagJoker
        );
        for (let i = 0; i < 3; i++) {
            assert.equal(
                Motely.decodeJokerRarity(cursor.getNext()),
                MotelyJokerRarity.Rare
            );
        }
    });

    it("tag stream second roll matches analyzer bigBlindTag", () => {
        const r = Motely.analyzeJamlSeeds(jaml.anyMust, ["AAAAAAAA"]);
        assert.ok(r.error == null);
        const analyzerBigBlind = r.seeds?.[0]?.analysis?.antes?.[0]?.bigBlindTag;
        assert.notEqual(analyzerBigBlind, null);

        const cursor = Motely.createStreamCursor(
            "AAAAAAAA",
            0,
            0,
            1,
            MotelyStreamKind.Tag
        );
        cursor.getNext();
        assert.equal(cursor.getNext(), analyzerBigBlind);
    });

    it("voucher stream yields even indices only with empty state", () => {
        assert.equal(typeof MotelyVoucher, "object");
        const maxVoucher = Math.max(
            ...Object.values(MotelyVoucher).filter((v) => typeof v === "number")
        );
        const cursor = Motely.createStreamCursor(
            "AAAAAAAA",
            0,
            0,
            1,
            MotelyStreamKind.Voucher
        );
        for (let i = 0; i < 5; i++) {
            const v = cursor.getNext();
            assert.ok(v >= 0 && v <= maxVoucher);
            assert.equal(v % 2, 0);
        }
    });

    it("packed int decoders round-trip on joker stream", () => {
        assert.equal(typeof MotelyItemType?.Joker, "number");
        assert.equal(typeof MotelyItemTypeCategory?.Joker, "number");

        const cursor = Motely.createStreamCursor(
            "AAAAAAAA",
            0,
            0,
            1,
            MotelyStreamKind.Joker
        );
        const v = cursor.getNext();

        assert.equal(typeof Motely.decodeItemType(v), "number");
        assert.equal(typeof Motely.decodeItemCategory(v), "number");
        assert.equal(typeof Motely.decodeJokerRarity(v), "number");
        assert.equal(typeof Motely.decodeItemEdition(v), "number");
        assert.equal(typeof Motely.decodeItemSeal(v), "number");
        assert.equal(typeof Motely.decodeItemEnhancement(v), "number");
        assert.equal(typeof Motely.isPerishable(v), "boolean");
        assert.equal(typeof Motely.isEternal(v), "boolean");
        assert.equal(typeof Motely.isRental(v), "boolean");
        assert.equal(
            Motely.decodeItemCategory(v),
            MotelyItemTypeCategory.Joker
        );
    });
});
