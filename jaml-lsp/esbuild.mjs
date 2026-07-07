import { build } from "esbuild";
import { execSync } from "node:child_process";
import { copyFileSync, mkdirSync, rmSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));

// Wipe stale output (e.g. leftover per-module files from an earlier tsc build) so the package
// ships only the two bundled entry points.
rmSync(join(here, "dist"), { recursive: true, force: true });

// jaml-lang is a file: dependency bundled into the output, so its dist/ must exist first.
execSync("npm run build", { cwd: join(here, "..", "jaml-lang"), stdio: "inherit" });

const common = {
  bundle: true,
  platform: "node",
  format: "cjs",
  target: "node20",
  sourcemap: true,
  // vscode is injected by the extension host, never bundled. motely-wasm is loaded at runtime
  // via a dynamic import esbuild can't see, so it's never pulled in here either.
  external: ["vscode"],
};

// The VS Code extension: CommonJS (the extension host does not support ESM extensions), with
// jaml-lang and vscode-languageserver-textdocument inlined so the installed VSIX needs no
// node_modules.
await build({
  ...common,
  entryPoints: [join(here, "src", "extension.ts")],
  outfile: join(here, "dist", "extension.js"),
});

// The standalone LSP server: spawned as `node dist/server.js --stdio` by any editor (Neovim, Zed,
// Claude Code's IDE integration). Fully self-contained, shebang'd so the bin entry is executable.
await build({
  ...common,
  entryPoints: [join(here, "src", "server.ts")],
  outfile: join(here, "dist", "server.js"),
  banner: { js: "#!/usr/bin/env node" },
});

// Claude Code plugin: ${CLAUDE_PLUGIN_ROOT} resolves to the plugin's own installed directory at
// runtime (non-configurable), so the bundled server must physically live there. Copying the
// already-built file (rather than a second build() with write:false) preserves the +x esbuild
// set on the shebang'd output — taking over the write ourselves would silently drop that.
const pluginDist = join(here, "..", "claude-plugin", "plugins", "jaml-lsp", "dist");
mkdirSync(pluginDist, { recursive: true });
copyFileSync(join(here, "dist", "server.js"), join(pluginDist, "server.js"));
