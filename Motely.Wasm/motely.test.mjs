#!/usr/bin/env node
/**
 * Publish gate for motely-wasm (see repo CLAUDE.md).
 * Run after: dotnet publish Motely.Wasm -c Release
 *
 *   node Motely.Wasm/motely.test.mjs
 *
 * Must exit 0 and print RESULT: PASS on success.
 * Override entry: MOTELY_WASM_ENTRY=/path/to/dist/index.mjs
 */
import { spawnSync } from "node:child_process";
import { readdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));
const testFiles = readdirSync(join(root, "tests"))
    .filter((name) => name.endsWith(".test.mjs"))
    .map((name) => join("tests", name));

const child = spawnSync(
    process.execPath,
    ["--test", "--test-concurrency=1", ...testFiles],
    {
        cwd: root,
        stdio: "inherit",
        env: process.env,
    }
);

const ok = child.status === 0;
console.log(ok ? "RESULT: PASS" : "RESULT: FAIL");
process.exit(child.status ?? 1);
