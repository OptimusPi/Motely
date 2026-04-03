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
const jamlSchemaSrc = resolve(__dirname, "..", "..", "..", "jaml.schema.json");
const jamlSchemaDst = resolve(__dirname, "jaml.schema.json");

function copyWasm() {
  if (!existsSync(wasmSrc)) {
    throw new Error("motely-wasm not found — run: pnpm install");
  }
  mkdirSync(resolve(__dirname, "dist"), { recursive: true });
  copyFileSync(wasmSrc, wasmDst);
  console.log("Copied motely-wasm.mjs ->", wasmDst);
}

function copyJamlSchema() {
  if (!existsSync(jamlSchemaSrc)) {
    throw new Error("jaml.schema.json not found in repo root");
  }
  copyFileSync(jamlSchemaSrc, jamlSchemaDst);
  console.log("Copied jaml.schema.json ->", jamlSchemaDst);
}

copyWasm();
copyJamlSchema();

// ── Extension host bundle ────────────────────────────────────────────────────
const ctx = await esbuild.context({
  entryPoints: ["src/extension.ts"],
  bundle: true,
  outfile: "dist/extension.js",
  external: ["vscode"],
  format: "cjs",
  platform: "node",
  sourcemap: true,
  minify: false,
  // motely-wasm is loaded at runtime via dynamic import from dist/
  // Redirect bare "motely-wasm" imports to the copied dist file at runtime
  plugins: [
    {
      name: "externalize-motely-wasm",
      setup(build) {
        // Catch bare specifier: import ... from "motely-wasm"
        build.onResolve({ filter: /^motely-wasm$/ }, () => ({
          path: "./motely-wasm.mjs",
          external: true,
        }));
        // Catch direct .mjs references
        build.onResolve({ filter: /motely-wasm\.mjs$/ }, () => ({
          path: "./motely-wasm.mjs",
          external: true,
        }));
      },
    },
  ],
});

// ── LSP server bundle (runs in a separate Node process via IPC) ──────────────
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
