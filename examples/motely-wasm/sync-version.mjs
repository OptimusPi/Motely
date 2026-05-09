// Reads <MotelyVersion> from ../Directory.Packages.props and writes it into
// package.json's version field AND any pinned `motely-wasm@<version>` URLs in
// README.md. Wired as the `prepack` lifecycle hook so it fires automatically
// on `npm pack` and `npm publish` — neither file is ever out of sync with the
// .NET canonical version at publish time.

import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const propsPath = join(here, "..", "Directory.Packages.props");
const pkgPath = join(here, "package.json");
const readmePath = join(here, "README.md");

const propsXml = readFileSync(propsPath, "utf8");
const match = propsXml.match(/<MotelyVersion>([^<]+)<\/MotelyVersion>/);
if (!match) {
    console.error(`sync-version: <MotelyVersion> not found in ${propsPath}`);
    process.exit(1);
}
const target = match[1].trim();

// package.json
const pkg = JSON.parse(readFileSync(pkgPath, "utf8"));
const previous = pkg.version;
if (previous === target) {
    console.log(`sync-version: package.json already at ${target}`);
} else {
    pkg.version = target;
    writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + "\n");
    console.log(`sync-version: package.json ${previous} -> ${target}`);
}

// README.md — rewrite every pinned `motely-wasm@<version>` reference.
// Matches unpkg, jsdelivr, and any other `motely-wasm@x.y.z` style pin.
const readme = readFileSync(readmePath, "utf8");
const versionPin = /motely-wasm@\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?/g;
const rewritten = readme.replace(versionPin, `motely-wasm@${target}`);
if (rewritten === readme) {
    console.log(`sync-version: README.md has no version pins to update`);
} else {
    writeFileSync(readmePath, rewritten);
    const before = (readme.match(versionPin) ?? []).length;
    console.log(`sync-version: README.md rewrote ${before} pin(s) -> ${target}`);
}
