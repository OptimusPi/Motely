#!/usr/bin/env node
// sync-version.mjs — reads <MotelyVersion> from Directory.Packages.props and writes to package.json files
import { readFileSync, writeFileSync, existsSync } from "fs";
import { resolve, dirname } from "path";
import { fileURLToPath } from "url";

const root = dirname(fileURLToPath(import.meta.url));

const props = readFileSync(resolve(root, "Directory.Packages.props"), "utf8");
const match = props.match(/<MotelyVersion>([^<]+)<\/MotelyVersion>/);
if (!match) { console.error("MotelyVersion not found in Directory.Packages.props"); process.exit(1); }
const version = match[1].trim();

const targets = [
  resolve(root, "Motely/package.json"),
  resolve(root, "Motely.npm-staging/motely-wasm/package.json"),
];

for (const pkgPath of targets) {
  if (!existsSync(pkgPath)) {
    console.warn(`  skip (missing): ${pkgPath}`);
    continue;
  }
  const pkg = JSON.parse(readFileSync(pkgPath, "utf8"));
  pkg.version = version;
  writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + "\n");
  console.log(`  ${pkgPath}: ${version}`);
}

console.log(`sync-version done: ${version}`);
