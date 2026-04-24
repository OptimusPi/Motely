import * as esbuild from "esbuild";
import { mkdirSync } from "fs";
import { resolve } from "path";
import { fileURLToPath } from "url";
import { dirname } from "path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const watch = process.argv.includes("--watch");
const production = process.argv.includes("--production");

mkdirSync(resolve(__dirname, "dist"), { recursive: true });

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
