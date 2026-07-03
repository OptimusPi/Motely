// Language-service tests: run against the compiled dist (npm test builds first via pretest).
// Focus: clause-level enum value validation + completion/hover parity, all sourced from the
// single generated ClauseKeyValueEnum table (no hand-kept per-file copies).
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { validate } from "../dist/validator.js";
import { getCompletions } from "../dist/completions.js";
import { getHover } from "../dist/hover.js";

const clause = (disc, key, val) => `must:\n  - ${disc}: Any\n    ${key}: ${val}\n`;
const enumDiags = (text) => validate(text).filter((d) => /Unknown Motely/.test(d.message));

describe("validator — clause-level enum values", () => {
  it("accepts valid enum scalars", () => {
    assert.equal(enumDiags(clause("standardCard", "seal", "Gold")).length, 0);
    assert.equal(enumDiags(clause("standardCard", "rank", "King")).length, 0);
    assert.equal(enumDiags(clause("standardCard", "suit", "Hearts")).length, 0);
    assert.equal(enumDiags(clause("joker", "edition", "Foil")).length, 0);
  });

  it("flags invalid enum scalars", () => {
    assert.match(enumDiags(clause("standardCard", "seal", "Bogus"))[0]?.message ?? "", /MotelyItemSeal/);
  });

  // Regression: `rank` used to be typed as a free string, so `rank: Garbage` passed silently
  // while `suit` (right next to it) was validated. Both must validate now.
  it("validates rank, not just suit (the drift bug)", () => {
    assert.equal(enumDiags(clause("standardCard", "rank", "Garbage")).length, 1);
    assert.equal(enumDiags(clause("standardCard", "suit", "Pentagons")).length, 1);
  });

  it("validates each element of array-valued keys (stickers)", () => {
    const text = "must:\n  - joker: Blueprint\n    stickers: [Eternal, Fake]\n";
    const diags = enumDiags(text);
    assert.equal(diags.length, 1, "flags only the bad element");
    assert.match(diags[0].message, /MotelyJokerSticker value 'Fake'/);
    assert.equal(enumDiags("must:\n  - joker: Blueprint\n    stickers: [Eternal, Rental]\n").length, 0);
  });

  it("allows Any anywhere", () => {
    assert.equal(enumDiags(clause("standardCard", "seal", "Any")).length, 0);
  });
});

describe("completions & hover — same source as the validator", () => {
  it("completes seal values from the enum", () => {
    const text = "must:\n  - standardCard: Any\n    seal: ";
    const labels = getCompletions(text, text.length).map((c) => c.label);
    assert.ok(labels.includes("Gold"), `expected Gold in ${labels.join(",")}`);
  });

  it("hovers rank with its enum (proves rank is wired, not just suit)", () => {
    const text = "must:\n  - standardCard: Any\n    rank: King";
    const hov = getHover(text, text.indexOf("King")); // hover the value
    assert.ok(hov && /MotelyStandardcardRank/.test(hov.markdown), "rank hover should name its enum");
  });
});
