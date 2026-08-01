import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { harness } from "./harness.mjs";
import { jaml, voucherSearch } from "./fixtures.mjs";

const { MotelyJaml } = harness;

describe("MotelyJaml", () => {
    it("validate returns null for valid JAML", () => {
        assert.equal(MotelyJaml.validate(voucherSearch("Overstock", ["AAAAAAAA"])), null);
    });

    it("validate returns an error string for garbage", () => {
        const err = MotelyJaml.validate(jaml.invalid);
        assert.equal(typeof err, "string");
        assert.ok(err.length > 0);
    });

    it("nativeFilterNames returns a non-empty list", () => {
        const names = MotelyJaml.nativeFilterNames();
        assert.ok(Array.isArray(names));
        assert.ok(names.length > 0);
    });
});
