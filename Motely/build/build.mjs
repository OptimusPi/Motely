#!/usr/bin/env node
/**
 * One command to build and publish both motely-wasm AND motely-node.
 *
 *   node build/build.mjs              → bump patch, build both, publish both
 *   node build/build.mjs --dry-run    → build both, npm publish --dry-run
 *   node build/build.mjs --tag-next   → publish to "next" tag
 *   node build/build.mjs --skip-bump  → don't bump version
 *
 * WASM: dotnet publish on Windows → Bootsharp LLVM → motely-wasm npm package
 * Node: dotnet publish in WSL Debian → NativeAOT linux-x64 → motely-node npm package
 */
import { execSync } from "node:child_process";
import { copyFileSync, existsSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(__dirname, "..", "..");
const propsPath = join(repoRoot, "Directory.Packages.props");
const nodeProjectPkgPath = join(repoRoot, "Motely.Run", "package.json");

const dryRun = process.argv.includes("--dry-run");
const tagNext = process.argv.includes("--tag-next");
const skipBump = process.argv.includes("--skip-bump");

function die(msg) { console.error(`\n[FATAL] ${msg}\n`); process.exit(1); }
function sh(cmd) { console.log(`  $ ${cmd}`); execSync(cmd, { stdio: "inherit", cwd: repoRoot, shell: true }); }
function wslDotnet(args) {
  sh(`wsl -d Debian -- bash -lc "export PATH=\\$HOME/.dotnet:\\$PATH; cd /mnt/x/JammySeedFinder/src/MotelyJAML; dotnet ${args}"`);
}

// ── Version ──────────────────────────────────────────────────────────────────

let propsXml = readFileSync(propsPath, "utf8");
const m = /<MotelyVersion>([^<]+)<\/MotelyVersion>/.exec(propsXml);
if (!m) die("MotelyVersion not found in Directory.Packages.props");

let [major, minor, patch] = m[1].trim().split(".").map(Number);
if ([major, minor, patch].some(Number.isNaN)) die(`Bad version: ${m[1]}`);

if (!skipBump) {
  const old = `${major}.${minor}.${patch}`;
  patch += 1;
  const ver = `${major}.${minor}.${patch}`;
  propsXml = propsXml.replace(`<MotelyVersion>${old}</MotelyVersion>`, `<MotelyVersion>${ver}</MotelyVersion>`);
  writeFileSync(propsPath, propsXml, "utf8");
  console.log(`[version] ${old} → ${ver}`);
}
const version = `${major}.${minor}.${patch}`;

// ── Auth check ───────────────────────────────────────────────────────────────

try { execSync("npm whoami", { stdio: "pipe" }); }
catch { die("Not logged in to npm. Run `npm login` first."); }

// ── Build both packages ──────────────────────────────────────────────────────

console.log(`\n[build] Building both motely-wasm (LLVM) and motely-node (NativeAOT)...`);
console.log(`[dotnet] WASM: dotnet publish Motely.Run -c Release`);
sh(`dotnet publish Motely.Run/Motely.Run.csproj -c Release`);

const templatePkg = JSON.parse(readFileSync(join(repoRoot, "Motely", "package.json"), "utf8"));
const nodeProjectPkgJson = {
  name: "motely-node",
  version,
  description: "Motely seed search - Node.js (NativeAOT linux-x64)",
  type: "module",
  main: "./bin/Release/net10.0/linux-x64/publish/Motely.Orchestration.cjs",
  types: "./bin/Release/net10.0/linux-x64/publish/Motely.Orchestration.d.ts",
  exports: {
    ".": {
      types: "./bin/Release/net10.0/linux-x64/publish/Motely.Orchestration.d.ts",
      import: "./bin/Release/net10.0/linux-x64/publish/Motely.Orchestration.mjs",
      require: "./bin/Release/net10.0/linux-x64/publish/Motely.Orchestration.cjs",
      default: "./bin/Release/net10.0/linux-x64/publish/Motely.Orchestration.cjs"
    }
  },
  files: ["bin/Release/net10.0/linux-x64/publish/*"],
  repository: templatePkg.repository,
  license: templatePkg.license,
  keywords: ["balatro", "seed", "node", "nativeaot", "linux-x64", "jaml"]
};
writeFileSync(nodeProjectPkgPath, JSON.stringify(nodeProjectPkgJson, null, 2) + "\n", "utf8");

console.log(`[dotnet] Node: dotnet publish in WSL Debian with NodeBuild`);
wslDotnet(`publish Motely.Run/Motely.Run.csproj -c Release -p:NodeBuild=true`);

// ── Validate build outputs ──────────────────────────────────────────────────

const wasmPkg = join(repoRoot, "motely-wasm");
if (!existsSync(join(wasmPkg, "index.mjs"))) die("Bootsharp WASM missing: motely-wasm/index.mjs");
if (!existsSync(join(wasmPkg, "types", "index.d.ts"))) die("Bootsharp WASM types missing: motely-wasm/types/index.d.ts");

const nodePkg = join(repoRoot, "Motely.Run", "motely-node");
const nodeTgz = join(nodePkg, `motely-node-${version}.tgz`);
if (!existsSync(nodeTgz)) die(`motely-node tarball missing: ${nodeTgz}`);

// ── Setup package.json versions ──────────────────────────────────────────────

const wasmPkgJson = JSON.parse(readFileSync(join(wasmPkg, "package.json"), "utf8"));
wasmPkgJson.version = version;
wasmPkgJson.exports = {
  ...(wasmPkgJson.exports ?? {}),
  "./jaml.schema.json": "./jaml.schema.json",
};
wasmPkgJson.files = Array.from(new Set([...(wasmPkgJson.files ?? []), "jaml.schema.json"]));

writeFileSync(join(wasmPkg, "package.json"), JSON.stringify(wasmPkgJson, null, 2) + "\n", "utf8");

// ── JAML schema ──────────────────────────────────────────────────────────────

console.log("[schema] Generate JAML schema");
sh(`dotnet run --project Motely.CLI/Motely.CLI.csproj -- --write-jaml-schema`);
if (!existsSync(join(repoRoot, "jaml.schema.json"))) die("jaml.schema.json not generated");
copyFileSync(join(repoRoot, "jaml.schema.json"), join(wasmPkg, "jaml.schema.json"));

// ── Publish both packages ────────────────────────────────────────────────────

const tag = tagNext ? "next" : "latest";
const pubArgs = `--access public --tag ${tag}${dryRun ? " --dry-run" : ""}`;

console.log(`\n[publish] motely-wasm@${version} → ${tag}${dryRun ? " (DRY RUN)" : ""}`);
execSync(`npm publish ${pubArgs}`, { stdio: "inherit", cwd: wasmPkg, shell: true });

console.log(`[publish] motely-node@${version} → ${tag}${dryRun ? " (DRY RUN)" : ""}`);
execSync(`npm publish "${nodeTgz}" ${pubArgs}`, { stdio: "inherit", cwd: repoRoot, shell: true });

console.log(`
════════════════════════════════════════════════════════════════
  ✓ motely-wasm@${version} → "${tag}"
  ✓ motely-node@${version} → "${tag}"
════════════════════════════════════════════════════════════════
`);
