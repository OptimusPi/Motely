import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";

const { bootsharp, MotelyWasm } = harness;

describe("runtime boot", () => {
    it("is Booted", () => {
        assert.equal(bootsharp.getStatus(), bootsharp.BootStatus.Booted);
    });

    it("MotelyWasm.getVersion returns a non-empty string", () => {
        const v = MotelyWasm.getVersion();
        assert.equal(typeof v, "string");
        assert.ok(v.length > 0, "version should be non-empty");
    });
});
