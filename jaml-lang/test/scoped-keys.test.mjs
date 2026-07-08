// Scoped key gating: a clause key is only valid relative to its discriminator.
// The flat AllClauseLevelKeys union is gone — these tests pin the behavior that
// replaced it, plus engine parity: every corpus filter the engine accepts must
// produce zero errors here.
import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { validate } from "../dist/validator.js";

const errors = (text) => validate(text).filter((d) => d.severity === "error");

describe("validator — discriminator-scoped clause keys", () => {
  it("rejects a key that is valid for another discriminator", () => {
    const diags = errors("must:\n  - joker: Blueprint\n    suit: Hearts\n");
    assert.equal(diags.length, 1);
    assert.match(diags[0].message, /'suit' is not valid for joker/);
  });

  it("accepts keys appearing before the discriminator (YAML maps are unordered)", () => {
    assert.equal(errors("must:\n  - antes: [1]\n    joker: Blueprint\n").length, 0);
  });

  it("flags a clause with no discriminator at all", () => {
    const diags = errors("must:\n  - antes: [1]\n    score: 5\n");
    assert.equal(diags.length, 1);
    assert.match(diags[0].message, /no discriminator/);
  });

  it("rejects a fully unknown key with the discriminator named", () => {
    const diags = errors("must:\n  - joker: Blueprint\n    jokr: Oops\n");
    assert.equal(diags.length, 1);
    assert.match(diags[0].message, /'jokr' is not valid for joker/);
  });

  it("stays silent inside a with block (no codegen vocabulary yet)", () => {
    const diags = errors("must:\n  - luckyMult: Any\n    with:\n      luck: 5\n");
    assert.equal(diags.length, 0);
  });

  it("ignores JUMMY plain-string clauses", () => {
    assert.equal(errors('must:\n  - "Eternal Blueprint in ante 1"\n').length, 0);
  });
});

describe("engine parity — JamlFilters corpus", () => {
  const corpusDir = join(import.meta.dirname, "..", "..", "JamlFilters");
  const files = readdirSync(corpusDir).filter((f) => f.endsWith(".jaml"));

  it("corpus exists and is non-trivial", () => {
    assert.ok(files.length > 0, "JamlFilters/ should contain .jaml files");
  });

  for (const file of files) {
    it(`accepts engine-valid corpus filter: ${file}`, () => {
      const text = readFileSync(join(corpusDir, file), "utf8");
      const diags = errors(text);
      assert.deepEqual(
        diags.map((d) => d.message),
        [],
        `${file} is engine-accepted but the validator flagged it`,
      );
    });
  }
});
