import { cpSync, existsSync, rmSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));

/** npm pack layout: <pkg>/dist/bootsharp — sibling dev: <pkg>/bootsharp */
function resolveVariantBase(packageRoot, variantDir) {
  const candidates = [
    resolve(packageRoot, "dist", variantDir),
    resolve(packageRoot, variantDir),
  ];
  return candidates.find((p) => existsSync(p));
}

const packageRoots = [
  resolve(root, "node_modules/motely-wasm"),
  resolve(root, "..", "motely-wasm"),
];

function findSource(variantDir) {
  for (const base of packageRoots) {
    if (!existsSync(base)) continue;
    const found = resolveVariantBase(base, variantDir);
    if (found) return found;
  }
  return null;
}

const copies = [
  { variant: "bootsharp", dst: "wwwroot/_framework" },
  { variant: "bootsharp_st", dst: "wwwroot/_framework_st" },
  { variant: "bootsharp", dst: "wwwroot/jammy-seed-finder/_framework" },
];

for (const { variant, dst } of copies) {
  const from = findSource(variant);
  if (!from) {
    console.warn(`skip missing source: ${variant}`);
    continue;
  }
  const to = resolve(root, dst);
  if (existsSync(to)) rmSync(to, { recursive: true, force: true });
  cpSync(from, to, { recursive: true });
  console.log(`${from} -> ${dst}`);
}
