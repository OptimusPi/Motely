// Local build smoke test — runs against the dotnet publish output in ../../motely-wasm/.
// Use after: dotnet publish Motely.Wasm -c Release

import bootsharp, { MotelyWasm } from "../../motely-wasm/index.mjs";

let failures = 0;
const expect = (name, ok, detail) => {
  if (ok) console.log(`  ok   ${name}`);
  else {
    console.log(`  FAIL ${name}${detail ? ` -- ${detail}` : ""}`);
    failures++;
  }
};

console.log("Booting motely-wasm (local build)...");
const t0 = Date.now();
await bootsharp.boot();
console.log(`Booted in ${Date.now() - t0}ms`);
console.log("");

console.log("Test 1: typed DTO clause (joker/uncommonJoker/boss/tarot)");
const validJaml = `
name: e2e-test
deck: Red
stake: White
must:
  - joker: Blueprint
  - uncommonJoker: Any
  - boss: TheArm
should:
  - tarot: TheEmperor
    score: 10
`;
const v1 = MotelyWasm.validateJamlStructured(validJaml);
expect("structured-validate succeeds (.valid=true)", v1.valid === true, JSON.stringify(v1));
expect("no error message", !v1.message, v1.message);

console.log("");
console.log("Test 2: case-insensitive enum parse");
const caseJaml = `
name: case-test
must:
  - joker: blueprint
  - boss: thearm
`;
const v2 = MotelyWasm.validateJamlStructured(caseJaml);
expect("lowercase enum names parse", v2.valid === true, JSON.stringify(v2));

console.log("");
console.log("Test 3: deprecated AnyUncommon wildcard is rejected");
const deprecatedJaml = `
name: deprecated-test
must:
  - joker: AnyUncommon
`;
const v3 = MotelyWasm.validateJamlStructured(deprecatedJaml);
expect("deprecated wildcard rejected (.valid=false)", v3.valid === false, JSON.stringify(v3));

console.log("");
console.log("Test 4: getJamlMeta counts typed clauses correctly");
const meta = MotelyWasm.getJamlMeta(validJaml);
expect("meta.mustCount === 3", meta.mustCount === 3, JSON.stringify(meta));
expect("meta.shouldCount === 1", meta.shouldCount === 1, JSON.stringify(meta));
expect("meta.deck === 'Red'", meta.deck === "Red", JSON.stringify(meta));
expect("meta.itemTypes includes UncommonJoker", meta.itemTypes.includes("UncommonJoker"), JSON.stringify(meta));

console.log("");
console.log("Test 5: getJamlSchema returns the live JSON Schema");
if (typeof MotelyWasm.getJamlSchema === "function") {
  const schemaJson = MotelyWasm.getJamlSchema();
  const schema = JSON.parse(schemaJson);
  expect("schema has $schema field", typeof schema.$schema === "string", schema.$schema);
  expect("schema has $defs.Joker enum", Array.isArray(schema?.$defs?.Joker?.enum), JSON.stringify(schema?.$defs?.Joker)?.slice(0, 80));
  expect("Joker enum includes Blueprint", schema.$defs.Joker.enum.includes("Blueprint"));
  expect("Joker enum includes 'any' wildcard", schema.$defs.Joker.enum.includes("any"));
  expect("schema has $defs.CommonJoker (Phase 2 narrowed)", Array.isArray(schema?.$defs?.CommonJoker?.enum));
} else {
  console.log("  skip - getJamlSchema not exported");
}

console.log("");
if (failures === 0) {
  console.log(`PASS - local build smoke clean.`);
  process.exit(0);
} else {
  console.log(`FAIL - ${failures} assertion(s) failed.`);
  process.exit(1);
}
