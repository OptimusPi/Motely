/**
 * Assembles npm artifacts from the Bootsharp / NativeAOT-LLVM WASM build.
 * LLVM + Emscripten settings live on Motely.Run.csproj; see:
 * https://bootsharp.com/guide/llvm
 */
import { execSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const motelyDir = join(__dirname, "..");
const repoRoot = join(motelyDir, "..");

function readMotelyVersion() {
  const props = readFileSync(join(repoRoot, "Directory.Packages.props"), "utf8");
  const m = /<MotelyVersion>([^<]+)<\/MotelyVersion>/.exec(props);
  if (!m) throw new Error("MotelyVersion not found in Directory.Packages.props");
  return m[1].trim();
}

function run(cmd, cwd = repoRoot) {
  execSync(cmd, { stdio: "inherit", cwd, env: process.env, shell: true });
}

const version = readMotelyVersion();
const csproj = join(repoRoot, "Motely.Run", "Motely.Run.csproj");
const cli = join(repoRoot, "Motely.CLI", "Motely.CLI.csproj");

run(`dotnet publish "${csproj}" -c Release`);

const wasmSrc = join(repoRoot, "Motely.Run", "bin", "motely-wasm");
const distWasm = join(motelyDir, "dist", "wasm");
const distRoot = join(motelyDir, "dist");

rmSync(distRoot, { recursive: true, force: true });
mkdirSync(distWasm, { recursive: true });
cpSync(wasmSrc, distWasm, { recursive: true });

run(`dotnet run --project "${cli}" -- --write-jaml-schema`);

const schemaDist = join(distRoot, "jaml.schema.json");
if (!existsSync(schemaDist)) {
  throw new Error("Motely/dist/jaml.schema.json missing after --write-jaml-schema");
}

const jamlSchemaMjs = `import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const raw = readFileSync(join(__dirname, "jaml.schema.json"), "utf8");
export default JSON.parse(raw);
`;
writeFileSync(join(distRoot, "jaml-schema.mjs"), jamlSchemaMjs, "utf8");

const jamlSchemaDts = `declare const schema: Record<string, unknown>;
export default schema;
`;
writeFileSync(join(distRoot, "jaml-schema.d.ts"), jamlSchemaDts, "utf8");

const indexDts = `export { default } from "./wasm/types/index";
export * from "./wasm/types/index";
`;
writeFileSync(join(distRoot, "index.d.ts"), indexDts, "utf8");

const nodeStub = `'use strict';
/** Native Node (NodeApi) build is produced with \`dotnet publish -p:NodeBuild=true\` on Linux CI. Browser consumers use \`motely\` → \`dist/wasm\`. */
module.exports = new Proxy(
  {},
  {
    get() {
      throw new Error(
        "[motely] Node native addon is not shipped in this package build. Use the browser export (dist/wasm) or publish Motely.Run with NodeBuild on linux-x64."
      );
    },
  }
);
`;
mkdirSync(join(distRoot, "node"), { recursive: true });
writeFileSync(join(distRoot, "node", "index.cjs"), nodeStub, "utf8");

const motelyPkg = JSON.parse(readFileSync(join(motelyDir, "package.json"), "utf8"));
motelyPkg.version = version;
writeFileSync(join(motelyDir, "package.json"), JSON.stringify(motelyPkg, null, 2) + "\n", "utf8");

const wasmStandalone = join(repoRoot, "motely-wasm");
rmSync(wasmStandalone, { recursive: true, force: true });
mkdirSync(wasmStandalone, { recursive: true });
cpSync(wasmSrc, wasmStandalone, { recursive: true });

const wasmPkg = {
  name: "motely-wasm",
  version,
  type: "module",
  description:
    "Bootsharp NativeAOT-LLVM WASM bundle for Motely (dotnet publish Motely.Run). See https://bootsharp.com/guide/llvm",
  main: "./index.mjs",
  types: "./types/index.d.ts",
  files: ["index.mjs", "types"],
  license: "MIT",
  repository: {
    type: "git",
    url: "git+https://github.com/OptimusPi/MotelyJAML.git",
    directory: "motely-wasm",
  },
  keywords: ["motely", "wasm", "bootsharp", "balatro", "nativeaot-llvm"],
};
writeFileSync(join(wasmStandalone, "package.json"), JSON.stringify(wasmPkg, null, 4) + "\n", "utf8");

console.log(`[motely build] OK — MotelyVersion=${version}, dist/wasm + motely-wasm synced.`);
