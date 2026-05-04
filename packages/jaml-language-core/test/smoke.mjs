import { strict as assert } from "node:assert";
import {
  JAML_FILE_EXTENSION,
  JAML_LANGUAGE_ID,
  JAML_SCHEMA_ID,
  JAML_CONTRACT,
  JAML_CRITERION_DEFINITION,
  JAML_CRITERION_SECTION_KEYS,
  analyzeJamlText,
  getJamlMeta,
  getJamlSchemaUrl,
  validateJamlWithMotely
} from "../index.js";

assert.equal(JAML_LANGUAGE_ID, "jaml");
assert.equal(JAML_FILE_EXTENSION, ".jaml");
assert.equal(JAML_SCHEMA_ID, "https://www.seedfinder.app/jaml.schema.json");
assert.equal(JAML_CRITERION_DEFINITION, "JamlClauseDto");
assert.deepEqual(JAML_CRITERION_SECTION_KEYS, ["must", "should", "mustNot"]);
assert.equal(JAML_CONTRACT.schemaId, JAML_SCHEMA_ID);
assert.deepEqual(JAML_CONTRACT.criterionSectionKeys, JAML_CRITERION_SECTION_KEYS);
assert.ok(getJamlSchemaUrl().endsWith("/packages/jaml-language-core/schema/jaml.schema.json"));

const validDiagnostics = validateJamlWithMotely("must: []", {
  validateJamlStructured: () => ({ valid: true, line: 0, column: 0 })
});
assert.deepEqual(validDiagnostics, []);

const invalidDiagnostics = validateJamlWithMotely("must:\n  - nope: true", {
  validateJamlStructured: () => ({
    valid: false,
    message: "Unknown criterion.",
    path: "$.must[0].nope",
    line: 2,
    column: 5
  })
});
assert.equal(invalidDiagnostics.length, 1);
assert.equal(invalidDiagnostics[0].source, "motely");
assert.equal(invalidDiagnostics[0].message, "Unknown criterion.");
assert.equal(invalidDiagnostics[0].path, "$.must[0].nope");
assert.deepEqual(invalidDiagnostics[0].range.start, { line: 1, character: 4 });

const missingValidatorDiagnostics = validateJamlWithMotely("must: []", undefined);
assert.equal(missingValidatorDiagnostics.length, 1);
assert.equal(missingValidatorDiagnostics[0].source, "jaml-language-core");

const meta = getJamlMeta("must: []", {
  validateJamlStructured: () => ({ valid: true, line: 0, column: 0 }),
  getJamlMeta: () => ({
    antes: Int32Array.from([1, 2]),
    itemTypes: ["Joker"],
    mustCount: 1,
    shouldCount: 0,
    mustNotCount: 0,
    deck: "Red",
    stake: "White"
  })
});
assert.equal(meta.deck, "Red");
assert.equal(meta.antes[1], 2);

const firstBuffoonDiagnostics = analyzeJamlText(`
must:
  - legendaryJoker: Any
    antes: [1]
    sources:
      boosterPacks: [0]
`);
assert.equal(firstBuffoonDiagnostics.length, 1);
assert.equal(firstBuffoonDiagnostics[0].code, "legendary-in-first-buffoon-pack");
assert.equal(firstBuffoonDiagnostics[0].severity, "warning");

const widePackDiagnostics = analyzeJamlText(`
must:
  - legendaryJoker: Any
    antes: [1]
    sources:
      boosterPacks: [0,1,2,3,4,5,6,7,8]
`);
assert.equal(widePackDiagnostics.some(d => d.code === "wide-ante-one-booster-range"), true);

const anteZeroDiagnostics = analyzeJamlText(`
must:
  - legendaryJoker: Perkeo
    antes: [0]
`);
assert.equal(anteZeroDiagnostics.length, 1);
assert.equal(anteZeroDiagnostics[0].code, "ante-zero-advanced-state");
assert.equal(anteZeroDiagnostics[0].severity, "information");

console.log("jaml-language-core smoke ok");
