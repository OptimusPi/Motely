#!/usr/bin/env node
// build.mjs — Cross-platform motely-wasm builder
// No PowerShell. No Windows paths. Just Node + dotnet.
// Usage: node build.mjs [publish]

import { execSync } from "node:child_process";
import { readFileSync, existsSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, "..");

const wasmDir = join(root, "Motely.Wasm");
const distDir = join(wasmDir, "dist");
const testFile = join(wasmDir, "motely.test.mjs");

function run(cmd, opts = {}) {
  console.log(`\n  $ ${cmd}`);
  execSync(cmd, { cwd: opts.cwd ?? root, stdio: "inherit", ...opts });
}

// ── 1. Get version ──────────────────────────────────────────────────
const props = readFileSync(join(root, "Directory.Packages.props"), "utf-8");
const versionMatch = props.match(/<MotelyVersion>([^<]+)<\/MotelyVersion>/);
const version = versionMatch?.[1] ?? "unknown";
console.log(`\n  MotelyVersion: ${version}`);

// ── 2. Clean stale output ────────────────────────────────────────────
console.log("\n  [1/4] Cleaning stale output...");
if (existsSync(distDir)) {
  run(`rm -rf "${distDir}"`, { cwd: wasmDir });
}

// ── 3. Restore ──────────────────────────────────────────────────────
console.log("\n  [2/4] Restoring NuGet packages...");
run("dotnet restore Motely.Wasm/Motely.Wasm.csproj");

// ── 4. Publish ──────────────────────────────────────────────────────
console.log("\n  [3/4] Publishing WASM...");
run("dotnet publish Motely.Wasm/Motely.Wasm.csproj -c Release");

// ── 5. Test ─────────────────────────────────────────────────────────
console.log("\n  [4/4] Smoke test...");
run(`node "${testFile}"`);

// ── 6. Publish? ─────────────────────────────────────────────────────
if (process.argv.includes("publish")) {
  console.log("\n  [5/5] Publishing to npm...");
  run("npm publish --access public", { cwd: wasmDir });
} else {
  console.log("\n  Add 'publish' arg to run: npm publish");
}

console.log(`\n  ✅ motely-wasm ${version} built successfully`);
