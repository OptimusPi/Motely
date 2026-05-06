import { mkdtempSync } from "node:fs";
import { mkdir, readFile, readdir, rename, rm, stat, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import bootsharp, { Bootsharp, Motely } from "../../motely-wasm/index.mjs";

const { MotelyWasm, MotelyWasmEvents } = Motely;

function parseArgs(argv) {
  const args = {
    runs: 3,
    mode: "sequential",
    batchCharCount: 4,
    startBatch: 0,
    endBatch: 1,
    randomSeedCount: 100000,
    jamlPath: null,
  };

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    const next = argv[i + 1];
    if (arg === "--runs" && next) {
      args.runs = Number(next);
      i++;
    } else if (arg === "--mode" && next) {
      args.mode = next;
      i++;
    } else if (arg === "--batch-char" && next) {
      args.batchCharCount = Number(next);
      i++;
    } else if (arg === "--start-batch" && next) {
      args.startBatch = Number(next);
      i++;
    } else if (arg === "--end-batch" && next) {
      args.endBatch = Number(next);
      i++;
    } else if (arg === "--random" && next) {
      args.randomSeedCount = Number(next);
      i++;
    } else if (arg === "--jaml" && next) {
      args.jamlPath = next;
      i++;
    }
  }

  return args;
}

function average(values) {
  return values.length === 0 ? 0 : values.reduce((a, b) => a + b, 0) / values.length;
}

function formatMs(value) {
  return `${value.toFixed(2)}ms`;
}

function formatSps(value) {
  return `${Math.round(value).toLocaleString()} seeds/sec`;
}

const options = parseArgs(process.argv.slice(2));

const defaultJaml = `
name: speed test
deck: Red
stake: White
must:
  - joker: Blueprint
    antes: [1, 2]
should:
  - uncommonJoker: Any
    antes: [1, 2, 3, 4]
    score: 10
  - rareJoker: Brainstorm
    antes: [1, 2, 3, 4]
    score: 25
`;

const stagedTempDir = options.jamlPath
  ? null
  : mkdtempSync(path.join(tmpdir(), "motely-speed-test-"));
const libraryRoot = options.jamlPath
  ? path.dirname(path.resolve(options.jamlPath))
  : stagedTempDir;
const libraryUri = options.jamlPath
  ? `/${path.basename(path.resolve(options.jamlPath))}`
  : "/speed-test.jaml";

if (stagedTempDir) {
  await writeFile(path.join(stagedTempDir, "speed-test.jaml"), defaultJaml, "utf8");
}

function uriToPath(dir, uri) {
  return path.join(dir, uri.replace(/^\//, ""));
}

function pathToUri(dir, abs) {
  return `/${path.relative(dir, abs).split(path.sep).join("/")}`;
}

async function scanDir(dir) {
  const out = [];

  async function walk(cur) {
    for (const ent of await readdir(cur, { withFileTypes: true })) {
      const abs = path.join(cur, ent.name);
      if (ent.isDirectory()) {
        out.push({ uri: pathToUri(dir, abs), file: false });
        await walk(abs);
      } else if (ent.isFile()) {
        out.push({ uri: pathToUri(dir, abs), file: true });
      }
    }
  }

  await walk(dir);
  return out;
}

console.log("Booting motely-wasm...");
const bootStart = performance.now();
await bootsharp.boot();
console.log(`Booted in ${formatMs(performance.now() - bootStart)}`);

Bootsharp.FileSystem.FileMounter.pickRoot = async () => libraryRoot;
Bootsharp.FileSystem.FileMounter.mount = async (root, watcher) => {
  const dir = root;
  const fs = {
    async createDirectory(uri) { await mkdir(uriToPath(dir, uri), { recursive: true }); },
    async removeDirectory(uri) { await rm(uriToPath(dir, uri), { recursive: true, force: true }); },
    async moveDirectory(fromUri, toUri) { await rename(uriToPath(dir, fromUri), uriToPath(dir, toUri)); },
    async getFileInfo(uri) {
      const s = await stat(uriToPath(dir, uri));
      return { type: "", bytesCount: Number(s.size), lastModified: s.mtime };
    },
    async readFile(uri) { return new Uint8Array(await readFile(uriToPath(dir, uri))); },
    async writeFile(uri, content) { await writeFile(uriToPath(dir, uri), Buffer.from(content)); },
    async deleteFile(uri) { await rm(uriToPath(dir, uri), { force: true }); },
    async moveFile(fromUri, toUri) { await rename(uriToPath(dir, fromUri), uriToPath(dir, toUri)); },
  };

  const entries = await scanDir(dir);
  const changes = [
    { type: 0, entry: { uri: "/", type: 1 } },
    ...entries.map(e => ({ type: 0, entry: { uri: e.uri, type: e.file ? 0 : 1 } })),
  ];
  await watcher.handleFileChanges(changes);
  return fs;
};
Bootsharp.FileSystem.FileMounter.unmount = async () => { };

MotelyWasmEvents.notifyProgress = () => { };
MotelyWasmEvents.notifyResult = () => { };
MotelyWasmEvents.notifyComplete = () => { };
MotelyWasmEvents.notifyJamlLibraryChanged = () => { };

const rootId = await MotelyWasm.mountJamlLibrary();
if (!rootId) {
  throw new Error("mountJamlLibrary returned null");
}

const jaml = await MotelyWasm.loadJamlFile(rootId, libraryUri);

const validation = MotelyWasm.validateJamlStructured(jaml);
if (!validation.valid) {
  throw new Error(`Invalid JAML: ${validation.message ?? "unknown error"}`);
}

console.log(`Mode: ${options.mode}`);
if (options.mode === "sequential") {
  console.log(`Sequential: batchChar=${options.batchCharCount}, startBatch=${options.startBatch}, endBatch=${options.endBatch}`);
} else {
  console.log(`Random: seeds=${options.randomSeedCount}`);
}
console.log(`Runs: ${options.runs}`);
console.log("");

const runSummaries = [];

for (let run = 1; run <= options.runs; run++) {
  let progressCount = 0;
  let resultCount = 0;
  let completeCount = 0;
  const progressTimes = [];
  const resultTimes = [];
  let completeStatus = null;

  MotelyWasmEvents.notifyProgress = () => {
    progressCount++;
    progressTimes.push(performance.now());
  };
  MotelyWasmEvents.notifyResult = () => {
    resultCount++;
    resultTimes.push(performance.now());
  };
  MotelyWasmEvents.notifyComplete = (status) => {
    completeCount++;
    completeStatus = status;
  };

  const wallStart = performance.now();
  const search = options.mode === "sequential"
    ? MotelyWasm.startSequentialSearch(
      jaml,
      options.batchCharCount,
      BigInt(options.startBatch),
      BigInt(options.endBatch),
    )
    : MotelyWasm.startRandomSearch(jaml, options.randomSeedCount);
  const completion = await search.waitForCompletion();
  const wallMs = performance.now() - wallStart;
  const snapshot = search.getSnapshot();
  const searched = Number(snapshot.totalSeedsSearched);
  const matched = Number(snapshot.matchingSeeds);
  const engineMs = Number(snapshot.elapsedMs);
  const wallSps = wallMs > 0 ? searched / (wallMs / 1000) : 0;
  const engineSps = engineMs > 0 ? searched / (engineMs / 1000) : 0;
  const progressDeltas = [];
  for (let i = 1; i < progressTimes.length; i++) {
    progressDeltas.push(progressTimes[i] - progressTimes[i - 1]);
  }

  const summary = {
    run,
    searched,
    matched,
    wallMs,
    engineMs,
    wallSps,
    engineSps,
    progressCount,
    resultCount,
    completeCount,
    completeStatus: completeStatus ?? completion.state,
    avgProgressDeltaMs: average(progressDeltas),
    firstProgressMs: progressTimes.length > 0 ? progressTimes[0] - wallStart : 0,
    lastProgressMs: progressTimes.length > 0 ? progressTimes[progressTimes.length - 1] - wallStart : 0,
    firstResultMs: resultTimes.length > 0 ? resultTimes[0] - wallStart : 0,
  };

  runSummaries.push(summary);

  console.log(`Run ${run}`);
  console.log(`  searched:  ${summary.searched.toLocaleString()}`);
  console.log(`  matched:   ${summary.matched.toLocaleString()}`);
  console.log(`  wall:      ${formatMs(summary.wallMs)} (${formatSps(summary.wallSps)})`);
  console.log(`  engine:    ${formatMs(summary.engineMs)} (${formatSps(summary.engineSps)})`);
  console.log(`  progress:  ${summary.progressCount} callbacks${summary.avgProgressDeltaMs > 0 ? `, avg Δ ${formatMs(summary.avgProgressDeltaMs)}` : ""}`);
  console.log(`  result:    ${summary.resultCount} callbacks`);
  console.log(`  complete:  ${summary.completeCount} (${summary.completeStatus ?? "none"})`);
  if (summary.firstProgressMs > 0) console.log(`  first progress: ${formatMs(summary.firstProgressMs)}`);
  if (summary.lastProgressMs > 0) console.log(`  last progress:  ${formatMs(summary.lastProgressMs)}`);
  if (summary.firstResultMs > 0) console.log(`  first result:   ${formatMs(summary.firstResultMs)}`);
  console.log("");
}

console.log("Summary");
console.log(`  avg wall:     ${formatMs(average(runSummaries.map(r => r.wallMs)))}`);
console.log(`  avg engine:   ${formatMs(average(runSummaries.map(r => r.engineMs)))}`);
console.log(`  avg wall sps: ${formatSps(average(runSummaries.map(r => r.wallSps)))}`);
console.log(`  avg eng sps:  ${formatSps(average(runSummaries.map(r => r.engineSps)))}`);
console.log(`  avg progress callbacks: ${average(runSummaries.map(r => r.progressCount)).toFixed(2)}`);
console.log(`  avg progress Δ: ${formatMs(average(runSummaries.map(r => r.avgProgressDeltaMs).filter(v => v > 0)))}`);

await MotelyWasm.unmountJamlLibrary(rootId);
if (stagedTempDir) {
  await rm(stagedTempDir, { recursive: true, force: true });
}
