import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";

const { MotelyLsp } = harness;

// The language brain in the browser: the same JamlLanguageService the stdio server hosts, so an
// editor — or an agent writing JAML through MCP — gets the engine's own answers, never a guess.
describe("MotelyLsp — engine language service over wasm", () => {
    it("diagnoses a clean document clean", () => {
        const diagnostics = MotelyLsp.diagnose(
            "deck: Red\nstake: White\nmust:\n  - joker: Blueprint\n"
        );
        assert.deepEqual(diagnostics, []);
    });

    it("reports a bad enum value with a code and a span", () => {
        const diagnostics = MotelyLsp.diagnose("deck: NotADeck\nstake: White\n");
        assert.equal(diagnostics.length, 1);
        assert.match(diagnostics[0].code, /^JAML/);
        assert.ok(diagnostics[0].message.length > 0);
        assert.ok(diagnostics[0].span);
    });

    it("accepts the terse clause line with continuation keys", () => {
        const diagnostics = MotelyLsp.diagnose("must:\n  - Negative Perkeo\n    ante: 1\n");
        assert.deepEqual(diagnostics, []);
    });

    it("hovers a discriminator from the schema", () => {
        const hover = MotelyLsp.hover("must:\n  - joker: Blueprint\n", 1, 5);
        assert.ok(hover, "no hover returned");
        assert.match(hover.markdown, /joker/);
    });

    it("completes engine vocabulary for a typed prefix", () => {
        const items = MotelyLsp.complete("must:\n  - joker: Blue\n", 1, 15);
        assert.ok(items.length > 0, "no completions");
        assert.ok(items.some(i => i.label === "Blueprint"), `Blueprint missing from [${items.map(i => i.label)}]`);
    });

    it("explains a discriminator from the generated schema", () => {
        const markdown = MotelyLsp.explain("voucher");
        assert.ok(markdown);
        assert.match(markdown, /MotelyVoucher/);
    });

    it("returns null explaining an unknown topic", () => {
        assert.equal(MotelyLsp.explain("zzzNotAThing"), null);
    });
});
