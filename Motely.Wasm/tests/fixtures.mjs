/** Shared JAML strings + helpers for motely-wasm interop tests. */

export const jaml = {
    // Two named seeds, no filter — for the analyzer.
    seeds: `name: t
deck: Red
stake: White
seeds: [UNITTEST, ALEEB]
`,
    // A should-clause makes JAMLyzer score each analyzed seed (result.score).
    scored: `name: t
deck: Red
stake: White
seeds: [UNITTEST, ALEEB]
should:
  - voucher: Telescope
    antes: [1, 2]
    score: 1
`,
    // One named seed — for resume/pagination, whose state bag is seed-specific.
    oneSeed: `name: t
deck: Red
stake: White
seeds: [UNITTEST]
`,
    // Ante 0 is the pre-run shop: a clause scoped to it analyzes it like any other ante.
    anteZero: `name: t
deck: Red
stake: White
seeds: [UNITTEST]
must:
  - legendaryJoker: Perkeo
    antes: [0, 1]
`,
    invalid: "not yaml !@#",
};

/** A real, discriminating filter: must have <voucherName> in ante 1, over the given seed list. */
export const voucherSearch = (voucherName, seeds) =>
    `name: t
deck: Red
stake: White
seeds: [${seeds.join(", ")}]
must:
  - voucher: ${voucherName}
    antes: [1]
`;
