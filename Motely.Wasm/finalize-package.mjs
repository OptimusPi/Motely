import { readFileSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";

const [version, pkgPath] = process.argv.slice(2);
if (!version || !pkgPath) {
    console.error("usage: finalize-package.mjs <version> <pkgJsonPath>");
    process.exit(1);
}

const file = resolve(pkgPath);
const pkg = JSON.parse(readFileSync(file, "utf8"));

pkg.version = version;
pkg.main = "./dist/index.mjs";
pkg.types = "./dist/index.d.mts";
pkg.exports = {
    ".": { types: "./dist/index.d.mts", import: "./dist/index.mjs" },
    "./node-boot": { types: "./dist/node-boot.d.mts", import: "./dist/node-boot.mjs" },
    "./*": {
        types: "./dist/generated/modules/*.g.d.mts",
        import: "./dist/generated/modules/*.g.mjs",
    },
};

writeFileSync(file, JSON.stringify(pkg, null, 2));
