import { cp, mkdir } from "node:fs/promises";
import { existsSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));

function findMotelyBin(startDir) {
  let dir = startDir;
  while (true) {
    const candidate = resolve(dir, "node_modules", "motely-wasm", "bin");
    if (existsSync(candidate)) return candidate;
    const parent = dirname(dir);
    if (parent === dir) return null;
    dir = parent;
  }
}

const src = findMotelyBin(resolve(here, ".."));
if (!src) {
  console.error("[copy-motely-bin] motely-wasm not installed");
  process.exit(1);
}

const dest = resolve(here, "..", "public", "motely-wasm", "bin");
await mkdir(dest, { recursive: true });
await cp(src, dest, { recursive: true });
console.log(`[copy-motely-bin] ${src} -> ${dest}`);
