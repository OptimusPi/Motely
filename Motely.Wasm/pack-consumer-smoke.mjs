#!/usr/bin/env node
/**
 * Consumer smoke: dotnet publish → npm pack → fresh install → import motely-wasm → boot(bin/) → validateJaml.
 * Run from repo root after a normal publish, or let this script publish first.
 *
 *   node Motely.Wasm/pack-consumer-smoke.mjs
 *
 * Skips publish when MOTELY_SKIP_PUBLISH=1 and motely-wasm/bin already exists.
 */
import { spawnSync } from "node:child_process";
import {
    mkdtemp,
    mkdir,
    writeFile,
    rm,
    readFile,
} from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { tmpdir } from "node:os";

const wasmProjectDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(wasmProjectDir, "..");
const packDir = join(wasmProjectDir, ".pack-consumer");
const motelyWasmDir = join(repoRoot, "motely-wasm");

function run(cmd, args, opts = {}) {
    const cmdToRun = (process.platform === "win32" && cmd.includes(" ")) ? `"${cmd}"` : cmd;
    const r = spawnSync(cmdToRun, args, {
        stdio: "inherit",
        cwd: opts.cwd ?? repoRoot,
        shell: process.platform === "win32",
        env: { ...process.env, ...opts.env },
    });
    if (r.status !== 0) {
        throw new Error(`${cmd} ${args.join(" ")} failed (${r.status})`);
    }
    return r;
}

async function main() {
    if (process.env.MOTELY_SKIP_PUBLISH !== "1") {
        console.log("dotnet publish Motely.Wasm -c Release …");
        run("dotnet", ["publish", join(wasmProjectDir, "Motely.Wasm.csproj"), "-c", "Release"]);
    }

    await mkdir(packDir, { recursive: true });
    console.log("npm pack motely-wasm …");
    const pack = spawnSync("npm", ["pack", "--pack-destination", packDir], {
        cwd: motelyWasmDir,
        encoding: "utf8",
        shell: process.platform === "win32",
    });
    if (pack.status !== 0) {
        throw new Error(`npm pack failed (${pack.status})`);
    }
    const tgzLine = pack.stdout.trim().split(/\r?\n/).filter(Boolean).pop();
    if (!tgzLine?.endsWith(".tgz")) {
        throw new Error(`npm pack: expected .tgz name, got: ${pack.stdout}`);
    }
    const tgzPath = join(packDir, tgzLine);

    const consumerDir = await mkdtemp(join(tmpdir(), "motely-wasm-consumer-"));
    try {
        await writeFile(
            join(consumerDir, "package.json"),
            JSON.stringify(
                {
                    name: "motely-wasm-consumer-smoke",
                    type: "module",
                    private: true,
                },
                null,
                2
            )
        );

        console.log(`npm install ${tgzPath} in ${consumerDir} …`);
        run("npm", ["install", tgzPath], { cwd: consumerDir });

        const runner = `
import bootsharp, { Motely } from "motely-wasm";
import { loadBootResourcesFromDir, resolvePackageBinDir } from "motely-wasm/node-boot";

await bootsharp.boot(await loadBootResourcesFromDir(resolvePackageBinDir()));

const jaml = \`
name: smoke
deck: Red
stake: White
must:
  - joker: WeeJoker
    antes: [1]
\`;

const status = Motely.validateJaml(jaml);
if (status !== "valid") throw new Error(status);
console.log("CONSUMER_SMOKE: PASS", Motely.version());
`;
        const runPath = join(consumerDir, "run.mjs");
        await writeFile(runPath, runner.trimStart());

        console.log("node run.mjs (installed package) …");
        run(process.execPath, [runPath], { cwd: consumerDir });
    } finally {
        await rm(consumerDir, { recursive: true, force: true });
    }
}

await main();
