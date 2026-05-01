// esbuild.mjs — bundles the LSP server into a single CJS file
import { build } from "esbuild";
import { argv } from "process";

const watch = argv.includes("--watch");

/** @type {import("esbuild").BuildOptions} */
const options = {
  entryPoints: ["src/server.mjs"],
  bundle: true,
  platform: "node",
  target: "node18",
  format: "cjs",
  outfile: "out/server.js",
  external: ["vscode"],
  // motely-wasm ships a pre-built ESM with embedded WASM binary —
  // mark it external so its internal import.meta handling stays intact,
  // then require() it at runtime via a CJS wrapper shim.
  banner: {
    js: `
// motely-wasm is ESM-only; load it via dynamic import at runtime.
const __importMotelyWasm = () => import("motely-wasm");
`.trimStart(),
  },
  logLevel: "info",
};

if (watch) {
  const ctx = await build({ ...options, sourcemap: true });
  await ctx.watch?.();
} else {
  await build(options);
}
