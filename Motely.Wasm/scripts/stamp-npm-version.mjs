// Stamp package.json's version from <MotelyVersion>, the single version source in
// Directory.Packages.props.
//
// This used to be an MSBuild target that regex-replaced the version inside the raw JSON
// text and wrote it back with WriteLinesToFile. That works on Windows and quietly corrupts
// the file everywhere else: MSBuild normalizes backslashes to forward slashes in property
// values, so every `\"` inside a script string came back as `/"` and package.json stopped
// being valid JSON. The build still went green, because nothing re-read the file.
//
// Parsing and re-serializing real JSON has no such opinion about backslashes.

import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const version = process.argv[2];
if (!version) {
  console.error("stamp-npm-version: pass the version as the first argument");
  process.exit(1);
}

const pkgPath = join(dirname(dirname(fileURLToPath(import.meta.url))), "package.json");
const pkg = JSON.parse(readFileSync(pkgPath, "utf8"));

if (pkg.version === version) {
  console.log(`package.json already at ${version}`);
} else {
  const previous = pkg.version;
  pkg.version = version;
  writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + "\n", "utf8");
  console.log(`stamped package.json ${previous} -> ${version}`);
}
