#!/usr/bin/env node
// build.mjs — build + pack motely-wasm and motely-node npm packages
// Usage:
//   node build.mjs wasm        — build motely-wasm only
//   node build.mjs node        — build motely-node only (needs VS dev tools or Linux)
//   node build.mjs             — build both
//   node build.mjs --pack      — build + npm pack both
//
// Version: single source of truth is <MotelyVersion> in Directory.Packages.props.
// Always use `npm pack ./motely-wasm` and `npm pack ./motely-node` (local folders).
// Plain `npm pack motely-wasm` can resolve npm registry and produce wrong/old tarballs.

import { execSync } from "child_process";
import { readFileSync, writeFileSync, cpSync, mkdirSync, existsSync, copyFileSync, readdirSync, unlinkSync } from "fs";
import { resolve, dirname } from "path";
import { fileURLToPath } from "url";

const root = dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const doPack = args.includes("--pack");
const targets = args.filter(a => !a.startsWith("--"));
const buildWasm = targets.length === 0 || targets.includes("wasm");
const buildNode = targets.length === 0 || targets.includes("node");

// ── Version sync ──────────────────────────────────────────────
const props = readFileSync(resolve(root, "Directory.Packages.props"), "utf8");
const vMatch = props.match(/<MotelyVersion>([^<]+)<\/MotelyVersion>/);
if (!vMatch) { console.error("MotelyVersion not found"); process.exit(1); }
const version = vMatch[1].trim();
console.log(`\n  MotelyVersion (Directory.Packages.props): ${version}\n`);

if (doPack) {
  console.log("  cleaning old motely-wasm-*.tgz / motely-node-*.tgz in repo root…");
  cleanOldNpmTarballs();
}

function syncVersion(pkgDir) {
  const pkgPath = resolve(root, pkgDir, "package.json");
  if (!existsSync(pkgPath)) return;
  const pkg = JSON.parse(readFileSync(pkgPath, "utf8"));
  pkg.version = version;
  writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + "\n");
  console.log(`  synced ${pkgDir}/package.json → ${version}`);
}

function run(cmd, label) {
  console.log(`\n── ${label} ──`);
  console.log(`  $ ${cmd}\n`);
  execSync(cmd, { cwd: root, stdio: "inherit" });
}

/** Remove prior motely-*.tgz in repo root so --pack leaves only one version pair. */
function cleanOldNpmTarballs() {
  const pat = /^(motely-wasm|motely-node)-.+\.tgz$/;
  for (const name of readdirSync(root)) {
    if (!pat.test(name)) continue;
    unlinkSync(resolve(root, name));
    console.log(`  removed old tarball: ${name}`);
  }
}

// ── WASM build ────────────────────────────────────────────────
if (buildWasm) {
  syncVersion("motely-wasm");

  const wasmCsproj = "Motely.Orchestration/Motely.Orchestration.csproj";
  const wasmProps = "-p:WasmBuild=true";

  run(`dotnet restore ${wasmCsproj} ${wasmProps}`, "motely-wasm: restore");
  run(`dotnet clean ${wasmCsproj} -c Release ${wasmProps}`, "motely-wasm: clean");
  run(`dotnet build ${wasmCsproj} -c Release ${wasmProps}`, "motely-wasm: build");
  run(`dotnet publish ${wasmCsproj} -c Release ${wasmProps}`, "motely-wasm: publish (NativeAOT-LLVM → browser-wasm)");

  // Stage bootsharp output → motely-wasm/dist/
  const src = resolve(root, "Motely.Orchestration/bin/bootsharp");
  const dst = resolve(root, "motely-wasm/dist");
  mkdirSync(dst, { recursive: true });
  copyFileSync(resolve(src, "index.mjs"), resolve(dst, "index.mjs"));
  if (existsSync(resolve(src, "types"))) {
    cpSync(resolve(src, "types"), resolve(dst, "types"), { recursive: true, force: true });
  }
  console.log(`  staged → motely-wasm/dist/`);

  if (doPack) {
    run("npm pack ./motely-wasm", "npm pack motely-wasm");
  }
}

// ── Node build ────────────────────────────────────────────────
if (buildNode) {
  syncVersion("motely-node");

  // Detect RID
  const isWin = process.platform === "win32";
  const rid = isWin ? "win-x64" : "linux-x64";

  const nodeCsproj = "Motely.Orchestration/Motely.Orchestration.csproj";
  const nodeProps = `-p:NodeBuild=true -r ${rid}`;

  run(`dotnet restore ${nodeCsproj} ${nodeProps}`, "motely-node: restore");
  run(`dotnet clean ${nodeCsproj} -c Release ${nodeProps}`, "motely-node: clean");
  run(`dotnet build ${nodeCsproj} -c Release ${nodeProps}`, "motely-node: build");
  run(`dotnet publish ${nodeCsproj} -c Release ${nodeProps}`, `motely-node: publish (NativeAOT → ${rid})`);

  // Stage output → motely-node/
  const pubDir = resolve(root, `Motely.Orchestration/bin/Release/net10.0/${rid}/publish`);
  const nodeDst = resolve(root, "motely-node");
  const ext = isWin ? ".dll" : ".so";

  // Copy .node, .d.ts, .cjs, .mjs
  for (const suffix of [".node", ".d.ts", ".cjs", ".mjs"]) {
    const file = resolve(pubDir, `Motely.Orchestration${suffix}`);
    if (existsSync(file)) {
      copyFileSync(file, resolve(nodeDst, `Motely.Orchestration${suffix}`));
      console.log(`  staged Motely.Orchestration${suffix}`);
    }
  }

  // Also check non-publish dir for generated files (.d.ts, .cjs, .mjs)
  const genDir = resolve(root, `Motely.Orchestration/bin/Release/net10.0/${rid}`);
  for (const suffix of [".d.ts", ".cjs", ".mjs"]) {
    const file = resolve(genDir, `Motely.Orchestration${suffix}`);
    const dst = resolve(nodeDst, `Motely.Orchestration${suffix}`);
    if (existsSync(file) && !existsSync(dst)) {
      copyFileSync(file, dst);
      console.log(`  staged Motely.Orchestration${suffix} (from gen)`);
    }
  }

  if (doPack) {
    run("npm pack ./motely-node", "npm pack motely-node");
  }
}

console.log("\n  done.\n");
