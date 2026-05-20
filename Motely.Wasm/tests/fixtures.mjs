/** Shared JAML strings and probe seeds for motely-wasm interop tests. */

/** `voucher: Any` / `joker: Any` in must/should are rejected; scoring fixtures name specific ids. */
export const jaml = {
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

/** Shop/pack/tag variety in analyzer output. */
export const probeSeeds = [
    "AAAAAAAA",
    "BBBBBBBB",
    "CCCCCCCC",
    "DDDDDDDD",
    "EEEEEEEE",
    "FFFFFFFF",
    "GGGGGGGG",
    "HHHHHHHH",
];
