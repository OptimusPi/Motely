// Live release smoke test — runs against motely-wasm AS PUBLISHED on npm.
// Boots the AOT-LLVM bundle, exercises the public JAML API across the
// breaking-change surface from v13.0.0 (typed DTOs, case-insensitive parse,
// deprecated-wildcard rejection), and asserts shape/content of getJamlMeta.
//
// Wired into build.ps1 via the `release-smoke` action; the `release` action
// runs it automatically after `npm publish` so every future bump self-verifies
// against the actually-published artifact (not a local build).

import { readFileSync } from "node:fs";
import bootsharp, { MotelyWasm } from "motely-wasm";

const packageJson = JSON.parse(
  readFileSync(new URL("./node_modules/motely-wasm/package.json", import.meta.url), "utf8"),
);

let failures = 0;
const expect = (name, ok, detail) => {
  if (ok) console.log(`  ok   ${name}`);
  else {
    console.log(`  FAIL ${name}${detail ? ` -- ${detail}` : ""}`);
    failures++;
  }
};

console.log("Booting motely-wasm...");
const t0 = Date.now();
await bootsharp.boot();
console.log(`Booted in ${Date.now() - t0}ms`);
console.log("");

console.log("Test 0: package/runtime/schema versions stay in lockstep");
const runtimeVersion = MotelyWasm.getVersion();
const releaseSchema = JSON.parse(MotelyWasm.getJamlSchema());
expect("runtime version equals package.json version", runtimeVersion === packageJson.version, `${runtimeVersion} !== ${packageJson.version}`);
expect("schema version equals package.json version", releaseSchema.version === packageJson.version, `${releaseSchema.version} !== ${packageJson.version}`);
expect("schema no longer exposes mixedJoker", !JSON.stringify(releaseSchema).includes("mixedJoker"));
console.log("");

console.log("Test 1: typed DTO clause (Phase 2 — joker/uncommonJoker/boss/tarot)");
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
console.log("Test 2: case-insensitive enum parse (Phase 1 — Enum.Parse ignoreCase)");
const caseJaml = `
name: case-test
must:
  - joker: blueprint
  - boss: thearm
`;
const v2 = MotelyWasm.validateJamlStructured(caseJaml);
expect("lowercase enum names parse", v2.valid === true, JSON.stringify(v2));

console.log("");
console.log("Test 3: deprecated AnyUncommon wildcard is rejected (Phase 1 cleanup)");
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
console.log("Test 5: getJamlSchema returns the live JSON Schema (lockstep with parser)");
if (typeof MotelyWasm.getJamlSchema === "function") {
  const schema = releaseSchema;
  expect("schema has $schema field", typeof schema.$schema === "string", schema.$schema);
  expect("schema has $defs.Joker enum", Array.isArray(schema?.$defs?.Joker?.enum), JSON.stringify(schema?.$defs?.Joker)?.slice(0, 80));
  expect("Joker enum includes Blueprint", schema.$defs.Joker.enum.includes("Blueprint"));
  expect("Joker enum includes 'any' wildcard", schema.$defs.Joker.enum.includes("any"));
  expect("schema has $defs.CommonJoker (Phase 2 narrowed)", Array.isArray(schema?.$defs?.CommonJoker?.enum));
} else {
  console.log("  skip - getJamlSchema not exported (pre-13.0.1 build)");
}

console.log("");
if (failures === 0) {
  console.log(`PASS - motely-wasm release smoke clean.`);
  process.exit(0);
} else {
  console.log(`FAIL - ${failures} assertion(s) failed.`);
  process.exit(1);
}
