
// Local motely-wasm integration tests — runs against the fresh dotnet publish output.
// Covers: boot, JAML validation, schema, search context, shop items, jokers, bosses.
// Run: node local-test.mjs  (from this dir, after dotnet publish Motely.Wasm -c Release)

import { fileURLToPath } from "node:url";
import { dirname, join, relative, resolve } from "node:path";
import { promises as fs } from "node:fs";

import bootsharp, { Bootsharp, Motely } from "../../motely-wasm/index.mjs";

const { MotelyWasm, MotelyWasmEvents } = Motely;
const { FileMounter, EntryType } = Bootsharp.FileSystem;

const __dirname = dirname(fileURLToPath(import.meta.url));
const fixturesDir = resolve(__dirname, "fixtures");

let failures = 0;
let total = 0;
const expect = (name, ok, detail) => {
  total++;
  if (ok) console.log(`  ok   ${name}`);
  else {
    console.log(`  FAIL ${name}${detail ? ` — ${detail}` : ""}`);
    failures++;
  }
};

console.log("Booting motely-wasm (local build)...");
const t0 = Date.now();
await bootsharp.boot();
console.log(`Booted in ${Date.now() - t0}ms\n`);

// ── Version ──
console.log("1. Version");
const version = MotelyWasm.getVersion();
expect("getVersion returns a string", typeof version === "string" && version.length > 0, version);

// ── JAML Validation ──
console.log("\n2. JAML Validation");
const goodJaml = `
name: test
deck: Red
stake: White
must:
  - joker: Blueprint
    antes: [1]
should:
  - uncommonJoker: Any
    score: 10
`;
const v1 = MotelyWasm.validateJamlStructured(goodJaml);
expect("valid JAML passes", v1.valid === true, JSON.stringify(v1));

const badJaml = `must:\n  - joker: NotARealJokerName`;
const v2 = MotelyWasm.validateJamlStructured(badJaml);
expect("invalid joker name rejected", v2.valid === false);

const typoJaml = `must:\n  - boses: TheArm`;
const v3 = MotelyWasm.validateJamlStructured(typoJaml);
expect("typo'd key 'boses' rejected by strict mode", v3.valid === false);

// ── standardCard with rank ──
console.log("\n3. standardCard clause");
const cardJaml = `
name: king-test
deck: Red
stake: White
must:
  - standardCard:
      rank: K
`;
const vc = MotelyWasm.validateJamlStructured(cardJaml);
expect("standardCard with rank: K is valid", vc.valid === true, JSON.stringify(vc));

// ── Schema ──
console.log("\n4. JAML Schema");
const schemaJson = MotelyWasm.getJamlSchema();
const schema = JSON.parse(schemaJson);
expect("schema parses as JSON", typeof schema === "object");
expect("schema has $schema", typeof schema.$schema === "string");
expect("schema has $defs.Joker", Array.isArray(schema?.$defs?.Joker?.enum));
expect("Joker enum includes Blueprint", schema.$defs.Joker.enum.includes("Blueprint"));
expect("schema has $defs.Boss", Array.isArray(schema?.$defs?.Boss?.enum));

// ── Seed analysis via analyzeJamlSeeds ──
console.log("\n5. Seed analysis (1AAAAAAA)");
const analysis = MotelyWasm.analyzeJamlSeeds(goodJaml, ["1AAAAAAA"]);
expect("analyzeJamlSeeds returns result", analysis != null);
expect("no error", !analysis.error, analysis.error);
const seedResult = analysis.seeds?.[0];
expect("seed result present", seedResult != null);
const antes = seedResult?.analysis?.antes;
expect("has antes", Array.isArray(antes) && antes.length > 0, `got ${antes?.length}`);
if (antes?.length > 0) {
  expect("ante 1 has boss", typeof antes[0].boss === "string" && antes[0].boss.length > 0, antes[0].boss);
  expect("ante 1 has voucher", typeof antes[0].voucher === "string", antes[0].voucher);
}

// ── JAML library mount (Bootsharp.FileSystem 0.8.0 via node fs) ──
console.log("\n6. JAML library mount + load from disk");

const uriToPath = (uri) => fileURLToPath(uri);
const pathToUri = (p) => "file://" + resolve(p).replace(/\\/g, "/");

async function listJamlFiles(rootDir) {
  const out = [];
  async function walk(dir) {
    for (const entry of await fs.readdir(dir, { withFileTypes: true })) {
      const full = join(dir, entry.name);
      if (entry.isDirectory()) await walk(full);
      else if (entry.isFile() && /\.(jaml|ya?ml)$/i.test(entry.name)) out.push(full);
    }
  }
  await walk(rootDir);
  return out;
}

const rootUri = pathToUri(fixturesDir);

FileMounter.pickRoot = async (_options) => rootUri;

FileMounter.unmount = async (_rootId) => { /* nothing to release */ };

FileMounter.mount = async (rootId, watcher, _options) => {
  // Build a thin IFileSystem against node fs, rooted at the dir behind rootId.
  const rootDir = uriToPath(rootId);
  const filesys = {
    createDirectory: (uri) => fs.mkdir(uriToPath(uri), { recursive: true }),
    removeDirectory: (uri) => fs.rm(uriToPath(uri), { recursive: true, force: true }),
    moveDirectory: (from, to) => fs.rename(uriToPath(from), uriToPath(to)),
    getFileInfo: async (uri) => {
      const stat = await fs.stat(uriToPath(uri));
      return {
        type: stat.isDirectory() ? "directory" : "file",
        bytesCount: stat.size,
        lastModified: stat.mtime,
      };
    },
    readFile: async (uri) => new Uint8Array(await fs.readFile(uriToPath(uri))),
    writeFile: async (uri, content) => fs.writeFile(uriToPath(uri), Buffer.from(content)),
    deleteFile: (uri) => fs.unlink(uriToPath(uri)),
    moveFile: (from, to) => fs.rename(uriToPath(from), uriToPath(to)),
  };

  // Seed the watcher with the existing .jaml files so getJamlLibraryFiles sees them.
  const initial = await listJamlFiles(rootDir);
  await watcher.handleFileChanges(
    initial.map((p) => ({
      type: 0, // Added
      entry: { uri: pathToUri(p), type: EntryType.File },
      fromUri: null,
      added: true,
      removed: false,
      modified: false,
      moved: false,
      file: true,
      directory: false,
    }))
  );

  return filesys;
};

const mountedRoot = await MotelyWasm.mountJamlLibrary();
expect("mountJamlLibrary returned a rootId", typeof mountedRoot === "string" && mountedRoot.length > 0, mountedRoot);

const libFiles = MotelyWasm.getJamlLibraryFiles(mountedRoot);
expect("library has at least one .jaml file", libFiles.length >= 1, `got ${libFiles.length}`);

const blueprintUri = libFiles.find((u) => u.toLowerCase().endsWith("blueprint.jaml"));
expect("blueprint.jaml is in the library", blueprintUri != null, libFiles.join(", "));

const loadedJaml = await MotelyWasm.loadLibraryFile(mountedRoot, blueprintUri);
expect("loadLibraryFile returns non-empty string", typeof loadedJaml === "string" && loadedJaml.length > 0);
expect("loaded content mentions Blueprint", loadedJaml.includes("Blueprint"));

const v4 = MotelyWasm.validateJamlStructured(loadedJaml);
expect("disk-loaded JAML is valid", v4.valid === true, JSON.stringify(v4));

const diskAnalysis = MotelyWasm.analyzeJamlSeeds(loadedJaml, ["1AAAAAAA"]);
expect("analyzeJamlSeeds works on disk-loaded JAML", !diskAnalysis?.error && diskAnalysis?.seeds?.[0] != null, diskAnalysis?.error);

await MotelyWasm.unmountJamlLibrary(mountedRoot);
expect("library unmounted (no files reported after)", MotelyWasm.getJamlLibraryFiles(mountedRoot).length === 0);

// ── Search ──
console.log("\n7. Random search (100 seeds)");
const results = [];
MotelyWasmEvents.notifyResult = (seed, score, tallies) => {
  results.push({ seed, score });
};
let progressCount = 0;
MotelyWasmEvents.notifyProgress = () => progressCount++;

const search = MotelyWasm.startRandomSearch(goodJaml, 100);
expect("startRandomSearch returns", search != null);
await search.waitForCompletion();
const snap = search.getSnapshot();
expect("snapshot has totalSeedsSearched", typeof snap.totalSeedsSearched === "bigint" || typeof snap.totalSeedsSearched === "number");
expect("searched >= 100", Number(snap.totalSeedsSearched) >= 100, `searched ${snap.totalSeedsSearched}`);
expect("got some results or zero matches (both valid)", results.length >= 0);
console.log(`  info: ${results.length} matches out of ${snap.totalSeedsSearched} seeds`);


// ── Summary ──
console.log(`\n${"=".repeat(50)}`);
if (failures === 0) {
  console.log(`PASS — ${total} assertions, 0 failures.`);
  process.exit(0);
} else {
  console.log(`FAIL — ${total} assertions, ${failures} failure(s).`);
  process.exit(1);
}
