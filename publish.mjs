#!/usr/bin/env node
// Usage:
//   node publish.mjs           — bump patch, build wasm + node, publish both
//   node publish.mjs --resume  — use current version (no bump), rebuild + publish both
//   node publish.mjs --node-only — use current version (no bump), skip wasm build/publish
import { execSync } from "child_process";
import { copyFileSync, mkdirSync, readFileSync, writeFileSync } from "fs";
import { resolve, dirname } from "path";
import { fileURLToPath } from "url";

const args = process.argv.slice(2);
const resume = args.includes("--resume") || args.includes("--node-only");
const nodeOnly = args.includes("--node-only");

const root = dirname(fileURLToPath(import.meta.url));
const propsPath = resolve(root, "Directory.Packages.props");
const props = readFileSync(propsPath, "utf8");
const m = props.match(/<MotelyVersion>(\d+)\.(\d+)\.(\d+)<\/MotelyVersion>/);
if (!m) { console.error("MotelyVersion not found"); process.exit(1); }
let [, maj, min, pat] = m.map(Number);
let next;
if (resume) {
    next = `${maj}.${min}.${pat}`;
    console.log(`resuming at ${next} (no version bump)`);
} else {
    pat++;
    next = `${maj}.${min}.${pat}`;
    writeFileSync(propsPath, props.replace(/<MotelyVersion>[^<]+</, `<MotelyVersion>${next}<`));
    console.log(`${m[1]}.${m[2]}.${m[3]} → ${next}`);
}

const csproj = "Motely.Orchestration/Motely.Orchestration.csproj";
const run = (cmd) => { console.log(`\n$ ${cmd}`); execSync(cmd, { cwd: root, stdio: "inherit" }); };
const syncVersion = (pkgDir) => {
    const pkgPath = resolve(root, pkgDir, "package.json");
    const pkg = JSON.parse(readFileSync(pkgPath, "utf8"));
    pkg.version = next;
    writeFileSync(pkgPath, JSON.stringify(pkg, null, 2) + "\n");
    console.log(`synced ${pkgDir}/package.json -> ${next}`);
};
const stageWasmSchema = () => {
    const schemaSource = resolve(root, "jaml.schema.json");
    const schemaTarget = resolve(root, "motely-wasm", "dist", "jaml.schema.json");
    mkdirSync(dirname(schemaTarget), { recursive: true });
    copyFileSync(schemaSource, schemaTarget);
    console.log(`staged ${schemaSource} -> ${schemaTarget}`);
    const staged = JSON.parse(readFileSync(schemaTarget, "utf8"));
    if (!staged.properties?.id || !staged.properties?.hashtags) {
        throw new Error("staged motely-wasm schema is missing id or hashtags");
    }
};

syncVersion("motely-wasm");
syncVersion("motely-node");
if (!nodeOnly) {
    run(`dotnet publish ${csproj} -c Release -p:WasmBuild=true`);
    run(`dotnet run --project Motely.CLI/Motely.CLI.csproj -c Release -- --write-jaml-schema`);
    stageWasmSchema();
    run(`npm publish ./motely-wasm`);
} else {
    console.log("\n[wasm] skipping wasm build+publish (--node-only)");
}
// Build motely-node inside Debian Bullseye (glibc 2.31) so the binary
// loads on Vercel's Amazon Linux 2023 (glibc 2.34). Building on Ubuntu 24.04
// produces a glibc 2.38 binary that dlopen-fails on Vercel every time.
console.log("\n[node] building in debian:bullseye (glibc 2.31) via Docker...");
run(
    `docker run --rm ` +
    `-v "${root}:/src" -w /src ` +
    `debian:bullseye-slim bash -c "` +
    `apt-get update -qq && ` +
    `apt-get install -yqq curl ca-certificates clang zlib1g-dev libicu-dev && ` +
    `curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir /usr/local/dotnet && ` +
    `export PATH=/usr/local/dotnet:$PATH && ` +
    `dotnet publish ${csproj} -c Release -p:NodeBuild=true -p:PackNpmPackage=false -p:MotelyVersion=${next}" `
);
run(`npm publish ./motely-node`);
