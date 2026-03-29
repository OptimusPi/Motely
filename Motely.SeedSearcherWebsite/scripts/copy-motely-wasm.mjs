#!/usr/bin/env node
import { cpSync, existsSync, mkdirSync, rmSync, statSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const src = join(root, "node_modules", "motely-wasm");
const dest = join(root, "motely-wasm");

if (!existsSync(src)) {
  console.error(
    "[copy-motely-wasm] Missing node_modules/motely-wasm — run npm install first."
  );
  process.exit(1);
}

if (existsSync(dest)) rmSync(dest, { recursive: true, force: true });
mkdirSync(dest, { recursive: true });
cpSync(src, dest, { recursive: true });
const bytes = statSync(join(dest, "index.mjs")).size;
console.log(`[copy-motely-wasm] → motely-wasm/ (${bytes} bytes index.mjs)`);
