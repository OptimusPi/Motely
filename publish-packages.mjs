#!/usr/bin/env node
// publish-packages.mjs — build, stage, dry-run, then publish both npm packages
//
// Usage:
//   node publish-packages.mjs              # dry-run only (safe, default)
//   node publish-packages.mjs --publish    # actually publish to npm
//   node publish-packages.mjs --tag beta   # publish with a dist-tag
//
// Prerequisites:
//   npm login                              # one-time, creates ~/.npmrc token
//   dotnet publish ...                     # build the binaries first (see below)

import { execSync } from "child_process";
import { existsSync, readdirSync } from "fs";
import { resolve, dirname } from "path";
import { fileURLToPath } from "url";

const root = dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const doPublish = args.includes("--publish");
const tagIdx = args.indexOf("--tag");
const tag = tagIdx !== -1 ? args[tagIdx + 1] : null;

function run(cmd, cwd) {
  console.log(`\n> ${cmd}`);
  execSync(cmd, { cwd, stdio: "inherit" });
}

function fatal(msg) {
  console.error(`\n  FATAL: ${msg}`);
  process.exit(1);
}

// ── 1. Sync version from Directory.Packages.props -> package.json files ──
console.log("\n=== Step 1: Sync versions ===");
run("node sync-version.mjs", root);

// ── 2. Verify motely-wasm has staged _framework ──
console.log("\n=== Step 2: Verify motely-wasm ===");
const wasmDir = resolve(root, "motely-wasm");
const fwDir = resolve(wasmDir, "_framework");
if (!existsSync(fwDir) || readdirSync(fwDir).length === 0) {
  fatal(
    "_framework/ is missing or empty.\n" +
    "  Run these first:\n" +
    "    dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release\n" +
    "    node stage-packages.mjs browser"
  );
}
console.log("  motely-wasm/_framework/ exists and is populated.");

// ── 3. Verify motely-node has at least one bin/ platform ──
console.log("\n=== Step 3: Verify motely-node ===");
const nodeDir = resolve(root, "motely-node");
const binDir = resolve(nodeDir, "bin");
if (!existsSync(binDir) || readdirSync(binDir).length === 0) {
  fatal(
    "bin/ is missing or empty. No native binaries found.\n" +
    "  Run at least one of:\n" +
    "    dotnet publish Motely.NodeAddon/Motely.NodeAddon.csproj -c Release -r win-x64\n" +
    "    dotnet publish Motely.NodeAddon/Motely.NodeAddon.csproj -c Release -r linux-x64\n" +
    "    dotnet publish Motely.NodeAddon/Motely.NodeAddon.csproj -c Release -r linux-musl-x64"
  );
}
const platforms = readdirSync(binDir);
console.log(`  motely-node/bin/ has platforms: ${platforms.join(", ")}`);

// ── 4. Dry-run both packages ──
console.log("\n=== Step 4: npm pack --dry-run ===");
run("npm pack --dry-run", wasmDir);
run("npm pack --dry-run", nodeDir);

if (!doPublish) {
  console.log("\n=== DRY RUN COMPLETE ===");
  console.log("Review the file lists above. If they look right, run:");
  console.log("  node publish-packages.mjs --publish");
  console.log("  node publish-packages.mjs --publish --tag beta");
  process.exit(0);
}

// ── 5. Publish ──
console.log("\n=== Step 5: Publishing to npm ===");
const tagFlag = tag ? ` --tag ${tag}` : "";

run(`npm publish${tagFlag}`, wasmDir);
console.log("  motely-wasm published.");

run(`npm publish${tagFlag}`, nodeDir);
console.log("  motely-node published.");

console.log("\n=== DONE ===");
