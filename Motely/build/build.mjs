#!/usr/bin/env node
/**
 * One command: build + publish motely + motely-wasm.
 *
 *   node build/build.mjs              → bump patch, build, publish
 *   node build/build.mjs --dry-run    → build, npm publish --dry-run
 *   node build/build.mjs --tag-next   → publish to "next" tag
 *   node build/build.mjs --skip-bump  → don't bump version
 *
 * Bootsharp generates the npm package (index.mjs, types/, package.json)
 * at Motely.Run/bin/motely-wasm/ automatically during dotnet publish.
 * This script just orchestrates: version bump → dotnet publish → schema → npm publish.
 *
 * https://bootsharp.com/guide/getting-started
 * https://bootsharp.com/guide/llvm
 */
import { execSync } from "node:child_process";
import { existsSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(__dirname, "..", "..");
const propsPath = join(repoRoot, "Directory.Packages.props");

const dryRun = process.argv.includes("--dry-run");
const tagNext = process.argv.includes("--tag-next");
const skipBump = process.argv.includes("--skip-bump");

function die(msg) { console.error(`\n[FATAL] ${msg}\n`); process.exit(1); }
function sh(cmd) { console.log(`  $ ${cmd}`); execSync(cmd, { stdio: "inherit", cwd: repoRoot, shell: true }); }

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

// ── Build (Bootsharp does the heavy lifting) ─────────────────────────────────

console.log("[build] dotnet publish Motely.Run → WASM + Bootsharp npm package");
sh(`dotnet publish Motely.Run/Motely.Run.csproj -c Release`);

const wasmPkg = join(repoRoot, "Motely.Run", "bin", "motely-wasm");
if (!existsSync(join(wasmPkg, "index.mjs"))) die("Bootsharp output missing: Motely.Run/bin/motely-wasm/index.mjs");
if (!existsSync(join(wasmPkg, "types", "index.d.ts"))) die("Bootsharp types missing");

// Copy template package.json to output directory
const templatePkg = JSON.parse(readFileSync(join(repoRoot, "Motely", "package.json"), "utf8"));
templatePkg.version = version;
writeFileSync(join(wasmPkg, "package.json"), JSON.stringify(templatePkg, null, 2) + "\n", "utf8");

// ── JAML schema ──────────────────────────────────────────────────────────────

console.log("[build] Generate JAML schema");
sh(`dotnet run --project Motely.CLI/Motely.CLI.csproj -- --write-jaml-schema`);
if (!existsSync(join(repoRoot, "jaml.schema.json"))) die("jaml.schema.json not generated");

// ── Publish motely-wasm (Bootsharp-generated package) ────────────────────────

const tag = tagNext ? "next" : "latest";
const pubArgs = `--access public --tag ${tag}${dryRun ? " --dry-run" : ""}`;

console.log(`\n[publish] motely-wasm@${version} → ${tag}${dryRun ? " (DRY RUN)" : ""}`);
execSync(`npm publish ${pubArgs}`, { stdio: "inherit", cwd: wasmPkg, shell: true });

console.log(`
════════════════════════════════════════
  motely-wasm@${version} ✓ → "${tag}"
════════════════════════════════════════
`);
