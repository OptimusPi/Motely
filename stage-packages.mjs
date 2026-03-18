#!/usr/bin/env node
// stage-packages.mjs — copies dotnet publish output into the npm package directories
// Usage: node stage-packages.mjs browser
import { cpSync, rmSync, existsSync } from "fs";
import { resolve, dirname } from "path";
import { fileURLToPath } from "url";

const root = dirname(fileURLToPath(import.meta.url));
const target = process.argv[2];

if (target === "browser") {
  const src = resolve(root, "Motely.BrowserWasm/bin/Release/net10.0-browser/publish/wwwroot/_framework");
  const dst = resolve(root, "motely-wasm/_framework");

  if (!existsSync(src)) {
    console.error(`publish output not found: ${src}`);
    console.error("Run: dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release");
    process.exit(1);
  }

  if (existsSync(dst)) rmSync(dst, { recursive: true });
  cpSync(src, dst, { recursive: true });
  console.log(`staged _framework -> motely-wasm/_framework`);
} else {
  console.error(`Usage: node stage-packages.mjs browser`);
  process.exit(1);
}
