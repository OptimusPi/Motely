// Bundles the JAML VS Code extension + language server into self-contained ESM.
//
// Two entry points:
//   - extension.ts → dist/extension.js   (runs in the VS Code extension host;
//                                          `vscode` is provided by the host → external)
//   - server.ts    → dist/server.js      (spawned as its own node process;
//                                          jaml-lang + vscode-languageserver are
//                                          bundled IN so the VSIX needs no node_modules)
import * as esbuild from "esbuild";

const watch = process.argv.includes("--watch");

/** @type {import('esbuild').BuildOptions} */
const shared = {
  bundle: true,
  format: "esm",
  platform: "node",
  target: "node20",
  sourcemap: true,
  // ESM output that uses __dirname/import.meta cleanly under node.
  banner: {
    js: "import { createRequire as __cr } from 'node:module'; const require = __cr(import.meta.url);",
  },
  logLevel: "info",
};

const configs = [
  { entryPoints: ["src/extension.ts"], outfile: "dist/extension.js", external: ["vscode"] },
  { entryPoints: ["src/server.ts"], outfile: "dist/server.js", external: [] },
];

if (watch) {
  const ctxs = await Promise.all(configs.map((c) => esbuild.context({ ...shared, ...c })));
  await Promise.all(ctxs.map((c) => c.watch()));
  console.log("esbuild: watching…");
} else {
  await Promise.all(configs.map((c) => esbuild.build({ ...shared, ...c })));
  console.log("esbuild: built dist/extension.js + dist/server.js");
}
