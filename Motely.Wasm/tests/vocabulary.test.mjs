import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";

const { MotelyJaml } = harness;

// The anti-hallucination tool: the vocabulary comes from the engine's own enums,
// so completions and agents can never drift from what the engine executes.
describe("MotelyJaml — vocabulary (listItems)", () => {
    it("finds LuckyCat from a partial word", () => {
        const hits = MotelyJaml.listItems("joker", "luckyc");
        assert.ok(hits.includes("LuckyCat"), `expected LuckyCat in [${hits}]`);
    });

    it("serves every kind with real engine names", () => {
        assert.equal(MotelyJaml.listItems("joker").length, 150, "all 150 jokers, exactly");
        assert.ok(MotelyJaml.listItems("deck").includes("Erratic"));
        assert.ok(MotelyJaml.listItems("stake").includes("Gold"));
        assert.ok(MotelyJaml.listItems("voucher").includes("Telescope"));
        assert.ok(MotelyJaml.listItems("edition").includes("Negative"));
        assert.ok(MotelyJaml.listItems("tarotCard").length > 0);
    });

    it("matches case-insensitively by substring", () => {
        assert.ok(MotelyJaml.listItems("voucher", "TELESC").includes("Telescope"));
    });

    it("rejects unknown kinds loudly, naming the valid ones", () => {
        assert.throws(() => MotelyJaml.listItems("pokemon")); // the throw crosses; C# exception text stays engine-side
    });

    it("short nicknames are not vocabulary kinds (path B)", () => {
        assert.throws(() => MotelyJaml.listItems("planet"));
        assert.throws(() => MotelyJaml.listItems("tarot"));
        assert.ok(MotelyJaml.listItems("planetCard").length > 0);
    });
});
