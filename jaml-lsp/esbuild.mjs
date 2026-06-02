import * as esbuild from "esbuild";

const production = process.argv.includes("--production");
const watch = process.argv.includes("--watch");

/** @type {import('esbuild').BuildOptions} */
const common = {
  bundle: true,
  platform: "node",
  target: "node18",
  format: "cjs",
  sourcemap: !production,
  minify: production,
  // `vscode` is provided by the host. `motely-wasm` is loaded at runtime (and is
  // optional) so the WASM glue is never bundled into the server.
  external: ["vscode", "motely-wasm"],
  logLevel: "info",
};

const targets = [
  { ...common, entryPoints: ["client/src/extension.ts"], outfile: "out/client/extension.js" },
  { ...common, entryPoints: ["server/src/server.ts"], outfile: "out/server/server.js" },
];

if (watch) {
  const contexts = await Promise.all(targets.map((t) => esbuild.context(t)));
  await Promise.all(contexts.map((c) => c.watch()));
  console.log("esbuild: watching…");
} else {
  await Promise.all(targets.map((t) => esbuild.build(t)));
  console.log("esbuild: build complete");
}
