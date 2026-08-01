import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";

const { MotelyJaml, MotelyUtilities, JamlAesthetic } = harness;

describe("MotelyJaml — one-line JAML", () => {
    it("canonicalizes the pinned Eternal Blueprint line", () => {
        assert.equal(MotelyJaml.validateLine("Eternal Blueprint in antes 1 or 2"), null);
        assert.equal(
            MotelyJaml.canonicalizeLine("Eternal Blueprint in antes 1 or 2"),
            "Eternal Blueprint in antes 1 or 2"
        );
    });

    it("canonicalizes ante tails and comma/or separators", () => {
        assert.equal(MotelyJaml.canonicalizeLine("Blueprint"), "Blueprint");
        assert.equal(MotelyJaml.canonicalizeLine("Blueprint in ante 1"), "Blueprint in ante 1");
        assert.equal(MotelyJaml.canonicalizeLine("Blueprint in antes 1 or 2"), "Blueprint in antes 1 or 2");
        assert.equal(MotelyJaml.canonicalizeLine("Showman in antes 1, 2"), "Showman in antes 1 or 2");
    });

    it("round-trips modifiers and consumables (no Any token — category any is block form)", () => {
        for (const line of [
            "Negative Blueprint in ante 1",
            "Eternal Perishable Showman in antes 2 or 3",
            "Foil Oops! All 6s in ante 1",
            "The Fool in ante 1",
            "The Emperor in antes 1 or 2",
            "Aura in ante 1",
            "Black Hole in antes 2 or 3",
            "Pluto in ante 1",
            "Planet X in antes 1-3",
        ]) assert.equal(MotelyJaml.canonicalizeLine(line), line);
        assert.notEqual(MotelyJaml.validateLine("Any in ante 1"), null);
    });

    it("round-trips standard cards, starting draw, vouchers, tags, bosses, and events", () => {
        for (const line of [
            "Red Seal Polychrome Steel King of Hearts in ante 1",
            "Gold Ace of Spades in antes 1 or 2",
            "Starting Draw King of Hearts in ante 1",
            "Voucher Telescope rolls 0 in ante 1",
            "Small Blind Tag Polychrome Tag in ante 1",
            "Boss The Wall in ante 8",
            "Lucky Money rolls 0 or 1 with luck 5",
            "Lucky Mult rolls 0 with luck 5",
            "Misprint Mult rolls 0 or 1 mult 1",
            "Wheel of Fortune rolls 0",
            "Business Payout rolls 0",
            "Bloodstone Trigger rolls 0",
            "Parking Payout rolls 0",
        ]) assert.equal(MotelyJaml.canonicalizeLine(line), line);
    });

    it("rejects invalid lines loudly", () => {
        const error = MotelyJaml.validateLine("Definitely Not A JAML Line");
        assert.equal(typeof error, "string");
        assert.ok(error.length > 0);
        assert.throws(() => MotelyJaml.canonicalizeLine("Definitely Not A JAML Line"));
    });
});

describe("MotelyUtilities", () => {
    it("round-trips total seed indices", () => {
        for (const [seed, index] of [["", 0n], ["1", 1n], ["9", 9n], ["A", 10n], ["Z", 35n], ["11", 36n], ["11111111", 66231629136n]]) {
            assert.equal(MotelyUtilities.seedToTotalIndex(seed), index);
            assert.equal(MotelyUtilities.totalIndexToSeed(index), seed);
        }
    });

    it("round-trips search seed indices", () => {
        for (const [seed, index] of [["11111111", 0n], ["11111112", 1n], ["1111111Z", 34n], ["11111121", 35n]]) {
            assert.equal(MotelyUtilities.seedToSearchIndex(seed), index);
            assert.equal(MotelyUtilities.searchIndexToSeed(index, seed.length), seed);
        }
    });

    it("uses inclusive search indices for batch range helpers", () => {
        assert.equal(MotelyUtilities.getFirstSeedOfLength(0), 0n);
        assert.equal(MotelyUtilities.getFirstSeedOfLength(1), 1n);
        assert.equal(MotelyUtilities.getFirstSeedOfLength(2), 36n);
        assert.equal(MotelyUtilities.maxSearchIndexInclusive(2), 35n * 35n - 1n);
        assert.equal(MotelyUtilities.seedToBatchIndex("11111111", 3), 0n);
        assert.equal(MotelyUtilities.batchIndexToSeedPrefix(0n, 3), "11111");
        assert.deepEqual([...MotelyUtilities.searchIndexRangeToBatchRange(0n, 34n, 1)], [0n, 1n]);
    });

    it("rejects invalid seed math inputs", () => {
        assert.throws(() => MotelyUtilities.seedToTotalIndex("10"));
        assert.throws(() => MotelyUtilities.totalIndexToSeed(-1n));
        assert.throws(() => MotelyUtilities.searchIndexRangeToBatchRange(0n, 1n, 0));
        assert.throws(() => MotelyUtilities.searchIndexRangeToBatchRange(2n, 1n, 1));
    });

    it("generates deterministic keyword sequences", () => {
        const repeats = MotelyUtilities.repeatCharKeywords(3);
        assert.equal(repeats[0], "AAA");
        assert.equal(repeats.at(-1), "ZZZ");
        assert.equal(repeats.length, 26);

        const ascending = MotelyUtilities.ascendingDigitLetterKeywords(4);
        const descending = MotelyUtilities.descendingDigitLetterKeywords(4);
        assert.equal(ascending[0], "1234");
        assert.equal(ascending.at(-1), "WXYZ");
        assert.equal(descending[0], "ZYXW");
        assert.equal(descending.at(-1), "4321");

        const mirrors = MotelyUtilities.mirrorPatternKeywords(2);
        assert.equal(mirrors.length, 13 * 13);
        assert.ok(mirrors.includes("AA"));
        assert.ok(mirrors.includes("88"));
    });

    it("pins keyword aesthetic counts and keyword validity", () => {
        // Counts from MotelySeedKeywordSequences (engine SoT).
        assert.equal(MotelyUtilities.getAestheticSeedCount(JamlAesthetic.Gross), 307252260n);
        assert.equal(MotelyUtilities.getAestheticSeedCount(JamlAesthetic.Funny), 493728588n);
        assert.equal(MotelyUtilities.getAestheticSeedCount(JamlAesthetic.Balatro), 913677733n);
        assert.equal(MotelyUtilities.getAestheticSeedCount(JamlAesthetic.Leet), 1525175581n);
        assert.equal(MotelyUtilities.getAestheticSeedCount(JamlAesthetic.Nsfw), 302944676n);
        // Every enum member resolves — pattern aesthetics have counts too.
        assert.ok(MotelyUtilities.getAestheticSeedCount(JamlAesthetic.Palindrome) > 0n);
        assert.ok(MotelyUtilities.getAestheticSeedCount(JamlAesthetic.Psychosis) > 0n);

        for (const keywords of [MotelyUtilities.grossKeywords(), MotelyUtilities.funnyKeywords(), MotelyUtilities.balatroKeywords()]) {
            assert.ok(keywords.length > 0);
            for (const keyword of keywords) {
                assert.ok(keyword.length >= 1 && keyword.length <= 8);
                assert.match(keyword.toUpperCase(), /^[1-9A-Z]+$/);
            }
        }
    });
});
