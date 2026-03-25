#!/usr/bin/env node
import { execSync } from "child_process";
import { copyFileSync, mkdirSync, readFileSync, writeFileSync } from "fs";
import { resolve, dirname } from "path";
import { fileURLToPath } from "url";

const root = dirname(fileURLToPath(import.meta.url));
const propsPath = resolve(root, "Directory.Packages.props");
const props = readFileSync(propsPath, "utf8");
const m = props.match(/<MotelyVersion>(\d+)\.(\d+)\.(\d+)<\/MotelyVersion>/);
if (!m) { console.error("MotelyVersion not found"); process.exit(1); }
let [, maj, min, pat] = m.map(Number);
pat++;
const next = `${maj}.${min}.${pat}`;
writeFileSync(propsPath, props.replace(/<MotelyVersion>[^<]+</, `<MotelyVersion>${next}<`));
console.log(`${m[1]}.${m[2]}.${m[3]} → ${next}`);

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
run(`dotnet publish ${csproj} -c Release -p:WasmBuild=true`);
run(`dotnet run --project Motely.CLI/Motely.CLI.csproj -c Release -- --write-jaml-schema`);
stageWasmSchema();
run(`npm publish ./motely-wasm`);
run(`dotnet publish ${csproj} -c Release -p:NodeBuild=true`);
run(`npm publish ./motely-node`);
