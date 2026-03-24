#!/usr/bin/env node
// build.mjs — build + publish motely-wasm and motely-node npm packages
//
// Usage:
//   node build.mjs wasm           — build motely-wasm only
//   node build.mjs node           — build motely-node only
//   node build.mjs                — build both
//   node build.mjs --publish      — bump + build + publish to npm
//   node build.mjs --bump         — bump patch version only
//   node build.mjs --bump minor   — bump minor version
//
// Version: <MotelyVersion> in Directory.Packages.props
//          → flows to <Version> via Directory.Build.props
//          → Bootsharp reads it for the generated package.json
//
// WASM: `dotnet publish` does everything (Bootsharp outputs to motely-wasm/)
// Node: `dotnet publish` + file staging (NodeApi, not Bootsharp)

import { execSync } from "child_process";
import { readFileSync, writeFileSync, existsSync, copyFileSync } from "fs";
import { resolve, dirname } from "path";
import { fileURLToPath } from "url";

const root = dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const doPublish = args.includes("--publish");
const doBump = args.includes("--bump") || doPublish;
const targets = args.filter(a => !a.startsWith("--") && !["major", "minor", "patch"].includes(a));
const buildWasm = targets.length === 0 || targets.includes("wasm");
const buildNode = targets.length === 0 || targets.includes("node");

function run(cmd, label) {
  console.log(`\n── ${label} ──`);
  console.log(`  $ ${cmd}\n`);
  execSync(cmd, { cwd: root, stdio: "inherit" });
}

function assertExists(path, label) {
  if (!existsSync(path)) throw new Error(`Missing ${label}: ${path}`);
}

// ── Version ──────────────────────────────────────────────────
const propsPath = resolve(root, "Directory.Packages.props");
let propsContent = readFileSync(propsPath, "utf8");
const vMatch = propsContent.match(/<MotelyVersion>(\d+)\.(\d+)\.(\d+)<\/MotelyVersion>/);
if (!vMatch) { console.error("MotelyVersion not found"); process.exit(1); }

if (doBump) {
  let [, maj, min, pat] = vMatch.map(Number);
  const kind = args.includes("major") ? "major" : args.includes("minor") ? "minor" : "patch";
  if (kind === "major") { maj++; min = 0; pat = 0; }
  else if (kind === "minor") { min++; pat = 0; }
  else { pat++; }
  const next = `${maj}.${min}.${pat}`;
  propsContent = propsContent.replace(/<MotelyVersion>[^<]+</, `<MotelyVersion>${next}<`);
  writeFileSync(propsPath, propsContent);
  console.log(`\n  version: ${vMatch[1]}.${vMatch[2]}.${vMatch[3]} → ${next}\n`);
} else {
  console.log(`\n  version: ${vMatch[1]}.${vMatch[2]}.${vMatch[3]}\n`);
}

// ── WASM (Bootsharp handles everything) ──────────────────────
if (buildWasm) {
  const csproj = "Motely.Orchestration/Motely.Orchestration.csproj";
  run(`dotnet publish ${csproj} -c Release -p:WasmBuild=true`, "motely-wasm: dotnet publish");

  assertExists(resolve(root, "motely-wasm/dist/index.mjs"), "motely-wasm entry");
  assertExists(resolve(root, "motely-wasm/dist/bootsharp/dotnet.js"), "motely-wasm bootsharp runtime");
  console.log("  motely-wasm: OK");

  if (doPublish) run("npm publish ./motely-wasm", "npm publish motely-wasm");
}

// ── Node (NodeApi — needs file staging) ──────────────────────
if (buildNode) {
  const isWin = process.platform === "win32";
  const rid = isWin ? "win-x64" : "linux-x64";
  const csproj = "Motely.Orchestration/Motely.Orchestration.csproj";

  run(`dotnet publish ${csproj} -c Release -p:NodeBuild=true -r ${rid}`, `motely-node: dotnet publish (${rid})`);

  // Stage .node, .d.ts, .cjs, .mjs into motely-node/
  const pubDir = resolve(root, `Motely.Orchestration/bin/Release/net10.0/${rid}/publish`);
  const genDir = resolve(root, `Motely.Orchestration/bin/Release/net10.0/${rid}`);
  const dst = resolve(root, "motely-node");

  for (const suffix of [".node", ".d.ts", ".cjs", ".mjs"]) {
    const src = resolve(pubDir, `Motely.Orchestration${suffix}`);
    const gen = resolve(genDir, `Motely.Orchestration${suffix}`);
    const target = resolve(dst, `Motely.Orchestration${suffix}`);
    if (existsSync(src)) { copyFileSync(src, target); console.log(`  staged Motely.Orchestration${suffix}`); }
    else if (existsSync(gen)) { copyFileSync(gen, target); console.log(`  staged Motely.Orchestration${suffix} (gen)`); }
  }

  // Sync version to motely-node/package.json
  const nodePkgPath = resolve(dst, "package.json");
  if (existsSync(nodePkgPath)) {
    const newVersion = propsContent.match(/<MotelyVersion>([^<]+)<\/MotelyVersion>/)[1];
    const pkg = JSON.parse(readFileSync(nodePkgPath, "utf8"));
    pkg.version = newVersion;
    writeFileSync(nodePkgPath, JSON.stringify(pkg, null, 2) + "\n");
  }

  assertExists(resolve(dst, "Motely.Orchestration.node"), "motely-node native addon");
  assertExists(resolve(dst, "Motely.Orchestration.mjs"), "motely-node ESM entry");
  console.log("  motely-node: OK");

  if (doPublish) run("npm publish ./motely-node", "npm publish motely-node");
}

console.log("\n  done.\n");
