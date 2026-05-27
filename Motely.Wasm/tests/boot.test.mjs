import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";

const { bootsharp } = harness;

describe("runtime boot", () => {
    it("remains Booted after the suite", () => {
        assert.equal(bootsharp.getStatus(), bootsharp.BootStatus.Booted);
    });
});
