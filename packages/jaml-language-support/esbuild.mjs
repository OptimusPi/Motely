// esbuild.mjs — bundles the VS Code extension into out/extension.js
// Bundles vscode-languageclient and other deps; keeps "vscode" external.
import { build } from "esbuild";
import { argv } from "process";

const watch = argv.includes("--watch");

/** @type {import("esbuild").BuildOptions} */
const options = {
  entryPoints: ["src/extension.cjs"],
  bundle: true,
  platform: "node",
  target: "node18",
  format: "cjs",
  outfile: "out/extension.js",
  external: ["vscode"],
  logLevel: "info",
};

if (watch) {
  const ctx = await build({ ...options, sourcemap: true });
  await ctx.watch?.();
} else {
  await build(options);
}
