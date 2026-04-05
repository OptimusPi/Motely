import * as esbuild from "esbuild";
import { copyFileSync, mkdirSync, existsSync } from "fs";
import { resolve } from "path";
import { fileURLToPath } from "url";
import { dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const watch = process.argv.includes("--watch");
const production = process.argv.includes("--production");

// ── Stage assets into the extension root / dist ─────────────────────────────

// 1. JAML JSON Schema → extension root (for contributes.yamlValidation).
const schemaSrc = resolve(__dirname, "..", "..", "..", "jaml.schema.json");
const schemaDst = resolve(__dirname, "jaml.schema.json");

// 2. WASM engine → dist/motely-wasm.mjs  (self-contained VSIX).
//    Tries: local dotnet publish output, then workspace node_modules.
const wasmCandidates = [
  resolve(__dirname, "..", "..", "..", "Motely.BrowserWasm", "motely-wasm-compat", "index.mjs"),
  resolve(__dirname, "..", "..", "..", "Motely.BrowserWasm", "motely-wasm", "index.mjs"),
  resolve(__dirname, "node_modules", "motely-wasm-compat", "index.mjs"),
  resolve(__dirname, "node_modules", "motely-wasm", "index.mjs"),
];
const wasmDst = resolve(__dirname, "dist", "motely-wasm.mjs");

function stageAssets() {
  mkdirSync(resolve(__dirname, "dist"), { recursive: true });

  // Schema (required)
  if (!existsSync(schemaSrc)) {
    throw new Error(
      `jaml.schema.json not found at ${schemaSrc} — run: dotnet run --project Motely.CLI -- --write-jaml-schema`
    );
  }
  copyFileSync(schemaSrc, schemaDst);
  console.log("Staged jaml.schema.json");

  // WASM (optional — extension still builds for LSP-only without it)
  const wasmSrc = wasmCandidates.find((p) => existsSync(p));
  if (wasmSrc) {
    copyFileSync(wasmSrc, wasmDst);
    console.log("Staged motely-wasm.mjs from", wasmSrc);
  } else {
    console.warn(
      "WARNING: No WASM engine found. The VSIX will have LSP + syntax only (no search).\n" +
      "To include search, run:  dotnet publish Motely.BrowserWasm -c Release"
    );
  }
}

stageAssets();

// ── esbuild ─────────────────────────────────────────────────────────────────

const shared = {
  bundle: true,
  format: "cjs",
  platform: "node",
  minify: production,
  sourcemap: production ? false : true,
  sourcesContent: false,
};

const ctx = await esbuild.context({
  ...shared,
  entryPoints: ["src/extension.ts"],
  outfile: "dist/extension.js",
  external: ["vscode"],
});

const lspCtx = await esbuild.context({
  ...shared,
  entryPoints: [resolve(__dirname, "..", "lsp-server", "src", "server.ts")],
  outfile: "dist/server.js",
});

if (watch) {
  await Promise.all([ctx.watch(), lspCtx.watch()]);
  console.log("Watching for changes\u2026");
} else {
  await Promise.all([ctx.rebuild(), lspCtx.rebuild()]);
  await Promise.all([ctx.dispose(), lspCtx.dispose()]);
  console.log("Build complete.");
}
