#!/usr/bin/env node
import { execSync } from "child_process";
import { readFileSync, writeFileSync } from "fs";
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

run(`dotnet publish ${csproj} -c Release -p:WasmBuild=true`);
run(`npm publish ./motely-wasm`);
run(`dotnet publish ${csproj} -c Release -p:NodeBuild=true`);
run(`npm publish ./motely-node`);
