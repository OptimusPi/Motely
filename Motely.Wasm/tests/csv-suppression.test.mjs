import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { voucherSearch } from "./fixtures.mjs";

const { MotelyJaml, MotelySearch, MotelyJamlyzer, MotelyVoucher } = harness;
const parse = (text) => MotelyJaml.fromYaml(text);

// The scorer ships its per-seed output — the `seed,score,tallies` CSV string — out through the SAME
// seedMatchCallback the bare no-scoring path uses. On a SCORING search, when a rich consumer
// (onScoredResult) is also listening, that CSV is redundant with the structured object and is the
// ONLY source of the double-emit. Run() withholds the CSV courier in exactly that case — and only
// that case (no rich consumer ⇒ the CSV must still flow, or the bare/CSV path would be dead).
//
// A real find: derive a true ante-1 voucher of AAAAAAAA from the analyzer, so the must-clause search
// genuinely matches AAAAAAAA and rejects BBBBBBBB — the events fire on real results, not a no-op.
function matchingVoucherSearch() {
    const [a] = MotelyJamlyzer.analyzeSeeds(parse("name: t\ndeck: Red\nstake: White\nseeds: [AAAAAAAA]\n"));
    const voucherName = MotelyVoucher[a.antes[0].voucher]; // numeric enum -> name
    return parse(voucherSearch(voucherName, ["AAAAAAAA", "BBBBBBBB"]));
}

describe("MotelySearch — redundant CSV suppression for rich consumers", () => {
    it("scoring search + onScoredResult attached ⇒ rich object delivered, CSV suppressed on onSeedMatch", async () => {
        const filter = matchingVoucherSearch();
        const scored = [];
        const csv = [];
        const onR = (r) => scored.push(r.seed);
        const onM = (s) => csv.push(s);
        MotelySearch.onScoredResult.subscribe(onR);
        MotelySearch.onSeedMatch.subscribe(onM);
        try {
            await MotelySearch.searchList(filter);
        } finally {
            MotelySearch.onScoredResult.unsubscribe(onR);
            MotelySearch.onSeedMatch.unsubscribe(onM);
        }

        assert.deepEqual(scored, ["AAAAAAAA"], "the rich scored object is still delivered");
        assert.deepEqual(csv, [], "the redundant CSV string is suppressed while a rich consumer listens");
    });

    it("scoring search + ONLY onSeedMatch attached ⇒ CSV still flows (the courier is not killed)", async () => {
        const filter = matchingVoucherSearch();
        const csv = [];
        const onM = (s) => csv.push(s);
        MotelySearch.onSeedMatch.subscribe(onM);
        try {
            await MotelySearch.searchList(filter);
        } finally {
            MotelySearch.onSeedMatch.unsubscribe(onM);
        }

        assert.equal(csv.length, 1, "the one matching seed is reported");
        assert.match(csv[0], /^AAAAAAAA,/, "as the seed,score,… CSV string when no rich consumer is attached");
    });
});
