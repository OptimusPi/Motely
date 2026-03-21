#!/usr/bin/env node
// stage-packages.mjs — copies Bootsharp publish output into motely-wasm
// Usage: node stage-packages.mjs bootsharp-st | bootsharp
import { cpSync, rmSync, existsSync, copyFileSync } from "fs";
import { resolve, dirname } from "path";
import { fileURLToPath } from "url";

const root = dirname(fileURLToPath(import.meta.url));
const target = process.argv[2];

const srcBase = resolve(root, "Motely.BrowserWasm/bin/bootsharp");
const jamlFiles = [
  ["Motely.NodeAddon/jaml-schema.js", "motely-wasm/jaml-schema.js"],
  ["Motely.NodeAddon/jaml-schema.d.ts", "motely-wasm/jaml-schema.d.ts"],
  ["jaml.schema.json", "motely-wasm/jaml.schema.json"],
];

function copyJamlArtifacts() {
  for (const [relSrc, relDst] of jamlFiles) {
    const from = resolve(root, relSrc);
    const to = resolve(root, relDst);
    if (!existsSync(from)) {
      console.warn(`skip jaml copy (missing): ${relSrc}`);
      continue;
    }
    copyFileSync(from, to);
    console.log(`  ${relSrc} -> ${relDst}`);
  }
}

if (target === "bootsharp-st") {
  const dst = resolve(root, "motely-wasm/bootsharp_st");
  if (!existsSync(srcBase)) {
    console.error(`Bootsharp output not found: ${srcBase}`);
    console.error("Run: dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release -p:SingleThread=true");
    process.exit(1);
  }
  if (existsSync(dst)) rmSync(dst, { recursive: true });
  cpSync(srcBase, dst, { recursive: true });
  copyJamlArtifacts();
  console.log(`staged ${srcBase} -> motely-wasm/bootsharp_st`);
} else if (target === "bootsharp") {
  const dst = resolve(root, "motely-wasm/bootsharp");
  if (!existsSync(srcBase)) {
    console.error(`Bootsharp output not found: ${srcBase}`);
    console.error("Run: dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release");
    process.exit(1);
  }
  if (existsSync(dst)) rmSync(dst, { recursive: true });
  cpSync(srcBase, dst, { recursive: true });
  copyJamlArtifacts();
  console.log(`staged ${srcBase} -> motely-wasm/bootsharp`);
} else {
  console.error(`Usage: node stage-packages.mjs bootsharp-st | bootsharp`);
  process.exit(1);
}
