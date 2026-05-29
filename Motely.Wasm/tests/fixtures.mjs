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

/** Shop/pack/tag variety in analyzer output. ALEEB is pinned in the C# JamlyzerUnitTests
 *  (`AnalyzeSeed_MarksJamlMatchedItemsForPreviewCards`) — its ante 1 has known matchable
 *  jokers in shop + Buffoon pack. Leading with it gives the analyzer tests a deterministic
 *  joker to find. */
export const probeSeeds = [
    "ALEEB",
    "ALEEBOOO",
    "AAAAAAAA",
    "BBBBBBBB",
    "CCCCCCCC",
    "DDDDDDDD",
    "EEEEEEEE",
    "FFFFFFFF",
    "GGGGGGGG",
    "HHHHHHHH",
];
