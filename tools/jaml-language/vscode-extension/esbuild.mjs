import * as esbuild from "esbuild";
import { copyFileSync, mkdirSync, existsSync } from "fs";
import { resolve } from "path";
import { fileURLToPath } from "url";
import { dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const watch = process.argv.includes("--watch");

// Copy motely-wasm/index.mjs from node_modules into dist/motely-wasm.mjs.
// The WASM binary is embedded inside (BootsharpEmbedBinaries=true) — single file, no extra assets.
const wasmSrc = resolve(__dirname, "node_modules/motely-wasm/index.mjs");
const wasmDst = resolve(__dirname, "dist/motely-wasm.mjs");

function copyWasm() {
  if (!existsSync(wasmSrc)) {
    throw new Error("motely-wasm not found — run: pnpm install");
  }
  mkdirSync(resolve(__dirname, "dist"), { recursive: true });
  copyFileSync(wasmSrc, wasmDst);
  console.log("Copied motely-wasm.mjs ->", wasmDst);
}

copyWasm();

const ctx = await esbuild.context({
  entryPoints: ["src/extension.ts"],
  bundle: true,
  outfile: "dist/extension.js",
  external: ["vscode"],
  format: "cjs",
  platform: "node",
  sourcemap: true,
  minify: false,
  // motely-wasm is loaded at runtime via createRequire/dynamic import
  // Keep it external so esbuild doesn't try to bundle the large .mjs
  plugins: [
    {
      name: "mark-motely-wasm-external",
      setup(build) {
        build.onResolve({ filter: /motely-wasm\.mjs$/ }, () => ({
          path: "./motely-wasm.mjs",
          external: true,
        }));
      },
    },
  ],
});

if (watch) {
  await ctx.watch();
  console.log("Watching for changes…");
} else {
  await ctx.rebuild();
  await ctx.dispose();
  console.log("Build complete.");
}
