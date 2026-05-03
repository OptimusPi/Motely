// Bootsharp.FileSystem (sponsor extension) — Node smoke test.
//
// The extension's real JS implementation (@rewaffle/bootsharp-file-system) only
// works in browsers — it sits on top of the File System Access API. To prove
// the C#-side wiring in MotelyWasmHost.MountJamlLibrary / LoadJamlFile /
// SaveJamlFile / GetJamlLibraryFiles, we provide a Node-side mock of
// `Bootsharp.FileSystem.FileMounter` backed by node:fs, then drive a full
// round trip: read a test JAML from disk, run a search, write the seeds
// back into the same library directory.
//
// Run: node filesystem-smoke.mjs   (from this dir, after dotnet publish Motely.Wasm -c Release)

import { mkdirSync, mkdtempSync, readFileSync, writeFileSync, existsSync, rmSync } from "node:fs";
import { readFile, writeFile, mkdir, rm, stat, rename } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";

import bootsharp, { Bootsharp, Motely } from "../../motely-wasm/index.mjs";

// On Bootsharp 0.8.0-alpha.111 the top-level export shape is { Bootsharp, Event, Motely, default }.
// `MotelyWasm` and `MotelyWasmEvents` are nested under `Motely`. (release-smoke.mjs and
// local-test.mjs still import them top-level — those are stale relative to this alpha.)
const MotelyWasm = Motely.MotelyWasm;
const MotelyWasmEvents = Motely.MotelyWasmEvents;

let failures = 0;
let total = 0;
const expect = (name, ok, detail) => {
    total++;
    if (ok) console.log(`  ok   ${name}`);
    else {
        console.log(`  FAIL ${name}${detail ? ` -- ${detail}` : ""}`);
        failures++;
    }
};

// ── 1. Stage a real on-disk library ──
const libRoot = mkdtempSync(path.join(tmpdir(), "motely-fs-smoke-"));
const testJamlPath = path.join(libRoot, "test.jaml");
const testJaml = `
name: filesystem-smoke
deck: Red
stake: White
must:
  - joker: Blueprint
    antes: [1]
should:
  - uncommonJoker: Any
    score: 10
`.trimStart();
writeFileSync(testJamlPath, testJaml, "utf8");
console.log(`Library staged at ${libRoot}\n`);

// ── 2. Node-side mock of Bootsharp.FileSystem.FileMounter ──
// URI convention (per upstream observer.ts): paths are relative to root and
// start with "/". Map "/foo/bar.jaml" → <libRoot>/foo/bar.jaml.
const mounts = new Map(); // rootId → { dir, watcher }
const uriToPath = (dir, uri) => path.join(dir, uri.replace(/^\//, ""));
const pathToUri = (dir, abs) => "/" + path.relative(dir, abs).split(path.sep).join("/");

async function scanDir(dir) {
    const out = [];
    async function walk(cur) {
        const { readdir } = await import("node:fs/promises");
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

Bootsharp.FileSystem.FileMounter.pickRoot = async () => libRoot;

Bootsharp.FileSystem.FileMounter.mount = async (root, watcher /*, options */) => {
    const dir = root; // we keyed rootId by absolute path for simplicity
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
    mounts.set(dir, { dir, watcher });

    // Mirror upstream observer.ts: emit Added (type=0) entries for the root and
    // every existing file/dir on initial mount. EntryType: 0=File, 1=Directory.
    const entries = await scanDir(dir);
    const changes = [
        { type: 0, entry: { uri: "/", type: 1 } },
        ...entries.map(e => ({ type: 0, entry: { uri: e.uri, type: e.file ? 0 : 1 } })),
    ];
    await watcher.handleFileChanges(changes);
    return fs;
};

Bootsharp.FileSystem.FileMounter.unmount = async (root) => { mounts.delete(root); };

// ── 3. Boot ──
console.log("Booting motely-wasm...");
const t0 = Date.now();
await bootsharp.boot();
console.log(`Booted in ${Date.now() - t0}ms\n`);

// ── 4. Mount + initial scan ──
// IMotelyWasmEvents is [Import]-style: each member is a JS function ref C# calls.
// (No .subscribe()/.unsubscribe() in alpha.111 — .d.ts says `export let notifyX: (...) => void`.)
console.log("1. Mount JAML library");
let libraryChangedCount = 0;
let lastChangedFiles = null;
const results = [];
MotelyWasmEvents.notifyProgress = () => { };
MotelyWasmEvents.notifyResult = (seed, score /*, tallies */) => results.push({ seed, score });
MotelyWasmEvents.notifyComplete = () => { };
MotelyWasmEvents.notifyJamlLibraryChanged = (rootId, fileUris) => {
    libraryChangedCount++;
    lastChangedFiles = [...fileUris];
};

const rootId = await MotelyWasm.mountJamlLibrary();
expect("mountJamlLibrary returns a rootId", typeof rootId === "string" && rootId.length > 0, String(rootId));
expect("notifyJamlLibraryChanged fired during mount", libraryChangedCount >= 1, `got ${libraryChangedCount}`);

const files = MotelyWasm.getJamlLibraryFiles(rootId);
expect("getJamlLibraryFiles lists /test.jaml", files.includes("/test.jaml"), JSON.stringify(files));
expect("library-changed event payload includes /test.jaml",
    Array.isArray(lastChangedFiles) && lastChangedFiles.includes("/test.jaml"),
    JSON.stringify(lastChangedFiles));

// ── 5. Read it back through C# ──
console.log("\n2. Load JAML through Bootsharp.FileSystem");
const loaded = await MotelyWasm.loadJamlFile(rootId, "/test.jaml");
expect("loadJamlFile returns the on-disk content verbatim", loaded === testJaml,
    `len ${loaded?.length} vs ${testJaml.length}`);

// Sanity: the loaded JAML actually validates.
const v = MotelyWasm.validateJamlStructured(loaded);
expect("loaded JAML validates", v.valid === true, JSON.stringify(v));

// ── 6. Run a search using the loaded JAML ──
console.log("\n3. Run random search using loaded JAML (200 seeds)");
const search = MotelyWasm.startRandomSearch(loaded, 200);
const snap = search.getSnapshot();
const totalSearched = Number(snap.totalSeedsSearched);
expect("search ran (>=200 seeds)", totalSearched >= 200, `searched ${totalSearched}`);
console.log(`  info: ${results.length} matches / ${totalSearched} seeds`);

// ── 7. Save seeds back into the library through C# ──
console.log("\n4. Save seeds through Bootsharp.FileSystem");
const seedsBlob = results.length === 0
    ? "(no matches in 200 seeds — search ran clean)\n"
    : results.map(r => `${r.seed}\t${r.score}`).join("\n") + "\n";

await MotelyWasm.saveJamlFile(rootId, "/results.txt", seedsBlob);

const onDiskPath = path.join(libRoot, "results.txt");
expect("results.txt exists on disk", existsSync(onDiskPath));
const onDisk = readFileSync(onDiskPath, "utf8");
expect("results.txt content matches what C# wrote", onDisk === seedsBlob,
    `disk ${onDisk.length}b vs sent ${seedsBlob.length}b`);

// ── 8. Unmount + cleanup ──
console.log("\n5. Unmount");
await MotelyWasm.unmountJamlLibrary(rootId);
expect("unmount removed the mount entry", !mounts.has(libRoot));

rmSync(libRoot, { recursive: true, force: true });

// ── Summary ──
console.log(`\n${"=".repeat(50)}`);
if (failures === 0) {
    console.log(`PASS - ${total} assertions, 0 failures.`);
    process.exit(0);
} else {
    console.log(`FAIL - ${total} assertions, ${failures} failure(s).`);
    process.exit(1);
}
