import * as esbuild from "esbuild";
import { copyFileSync, mkdirSync, existsSync } from "fs";
import { resolve } from "path";
import { fileURLToPath } from "url";
import { dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const watch = process.argv.includes("--watch");

// VSCE uses --no-dependencies + pnpm: ship runtime bits into the VSIX explicitly.
const wasmSrc = resolve(__dirname, "node_modules", "motely-wasm-compat", "index.mjs");
const wasmDst = resolve(__dirname, "dist", "motely-wasm-compat.mjs");
const schemaSrc = resolve(__dirname, "node_modules", "@motely", "jaml-schema", "jaml.schema.json");
const schemaDst = resolve(__dirname, "jaml.schema.json");

function stagePackagedAssets() {
  if (!existsSync(wasmSrc)) {
    throw new Error(
      "motely-wasm-compat not found — pnpm install (tools/jaml-language); dotnet publish Motely.BrowserWasm for file: link."
    );
  }
  if (!existsSync(schemaSrc)) {
    throw new Error(
      "@motely/jaml-schema not found — pnpm install (tools/jaml-language). prepare syncs jaml.schema.json."
    );
  }
  mkdirSync(resolve(__dirname, "dist"), { recursive: true });
  copyFileSync(wasmSrc, wasmDst);
  copyFileSync(schemaSrc, schemaDst);
  console.log("Staged motely-wasm-compat ->", wasmDst);
  console.log("Staged @motely/jaml-schema ->", schemaDst);
}

stagePackagedAssets();

const ctx = await esbuild.context({
  entryPoints: ["src/extension.ts"],
  bundle: true,
  outfile: "dist/extension.js",
  external: ["vscode", "motely-wasm-compat"],
  format: "cjs",
  platform: "node",
  sourcemap: true,
  minify: false,
  plugins: [
    {
      name: "resolve-motely-wasm-compat",
      setup(build) {
        build.onResolve({ filter: /^motely-wasm-compat$/ }, () => ({
          path: "./motely-wasm-compat.mjs",
          external: true,
        }));
      },
    },
  ],
});

const lspCtx = await esbuild.context({
  entryPoints: [resolve(__dirname, "..", "lsp-server", "src", "server.ts")],
  bundle: true,
  outfile: "dist/server.js",
  format: "cjs",
  platform: "node",
  sourcemap: true,
  minify: false,
});

if (watch) {
  await Promise.all([ctx.watch(), lspCtx.watch()]);
  console.log("Watching for changes…");
} else {
  await Promise.all([ctx.rebuild(), lspCtx.rebuild()]);
  await Promise.all([ctx.dispose(), lspCtx.dispose()]);
  console.log("Build complete.");
}
