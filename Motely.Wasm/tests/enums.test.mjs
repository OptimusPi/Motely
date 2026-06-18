import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";

// Regression guard for the "enums silently dropped" bug class.
//
// motely-wasm once exported the full Motely enum vocabulary to JS. A refactor that
// hid the typed clause model behind an interface (by-reference interop instead of
// by-value serialization) silently stopped Bootsharp from emitting the enum
// name<->index maps for everything except MotelyDeck/MotelyStake — and consumers
// were told to pin a major-and-a-half-old version. There was no test, so the drop
// was invisible until prod. These turn that silent drop into a red build.
//
// Each enum reaches `motely/enums` only if it appears in a marshalled [Export]/[Import]
// signature on a by-value type; the harness spreads that module, so harness[name] is
// the enum object (or undefined if it never crossed).

const REQUIRED_ENUMS = [
    "MotelyDeck",
    "MotelyStake",
    "MotelyJoker",
    "MotelyVoucher",
    "MotelyTarotCard",
    "MotelySpectralCard",
    "MotelyPlanetCard",
    "MotelyBossBlind",
    "MotelyTag",
    "MotelyItemEdition",
    "MotelyItemSeal",
    "MotelyItemEnhancement",
    "MotelyEventType",
    "MotelyBoosterPack",
];

describe("exported enum vocabulary", () => {
    for (const name of REQUIRED_ENUMS) {
        it(`exports ${name} with named members`, () => {
            const e = harness[name];
            assert.ok(
                e && typeof e === "object",
                `${name} is not exported from motely/enums — Bootsharp dropped its map`,
            );
            // Bootsharp emits a bidirectional map; the name->index direction has string keys.
            const named = Object.keys(e).filter((k) => Number.isNaN(Number(k)));
            assert.ok(named.length > 0, `${name} exported but has no named members`);
        });
    }

    it("MotelyVoucher contains a concrete known member", () => {
        const v = harness.MotelyVoucher;
        assert.ok(v && typeof v === "object", "MotelyVoucher is not exported");
        // Telescope is a real voucher (used in the JSON loader tests); pinning a concrete
        // member catches a truncated/renamed enum, not just a present-but-empty object.
        assert.ok("Telescope" in v, "MotelyVoucher.Telescope missing");
    });
});
