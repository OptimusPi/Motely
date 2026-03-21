import { cpSync, existsSync, rmSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));
const packageRoots = [
  resolve(root, "node_modules/motely-wasm"),
  resolve(root, "..", "motely-wasm"),
];

const copies = [
  { src: "bootsharp", dst: "wwwroot/_framework" },
  { src: "bootsharp_st", dst: "wwwroot/_framework_st" },
  { src: "bootsharp", dst: "wwwroot/jammy-seed-finder/_framework" },
];

for (const { src, dst } of copies) {
  const to = resolve(root, dst);
  const from = packageRoots
    .map(base => resolve(base, src))
    .find(candidate => existsSync(candidate));
  if (!from) {
    console.warn(`skip missing source: ${src}`);
    continue;
  }
  if (existsSync(to)) rmSync(to, { recursive: true, force: true });
  cpSync(from, to, { recursive: true });
  console.log(`${from} -> ${dst}`);
}
