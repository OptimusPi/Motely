/**
 * vendor.mjs
 * Builds jaml-lsp-server and copies it (plus motely-wasm) into
 * vendor/jaml-lsp-server/ so the packaged extension can find both.
 *
 * Run: node ./scripts/vendor.mjs
 *
 * Output layout:
 *   vendor/jaml-lsp-server/out/server.js          ← bundled LSP server (CJS)
 *   vendor/jaml-lsp-server/node_modules/motely-wasm/  ← so dynamic import resolves
 */

import { execSync } from "node:child_process";
import { cpSync, mkdirSync, rmSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const __dir = dirname(fileURLToPath(import.meta.url));
const root = join(__dir, "..");
const lspDir = join(root, "..", "jaml-lsp-server");
const vendorLsp = join(root, "vendor", "jaml-lsp-server");
const schemaDst = join(root, "schema", "jaml.schema.json");

console.log("→ Installing jaml-lsp-server deps…");
execSync("npm install --prefer-offline", { cwd: lspDir, stdio: "inherit" });

console.log("→ Building jaml-lsp-server…");
execSync("npm run build", { cwd: lspDir, stdio: "inherit" });

console.log("→ Copying server bundle to vendor…");
rmSync(join(vendorLsp, "out"), { recursive: true, force: true });
mkdirSync(join(vendorLsp, "out"), { recursive: true });
cpSync(
  join(lspDir, "out", "server.js"),
  join(vendorLsp, "out", "server.js")
);

console.log("→ Vendoring motely-wasm next to bundled server…");
const motelyWasmSrc = join(lspDir, "node_modules", "motely-wasm");
const motelyWasmDst = join(vendorLsp, "node_modules", "motely-wasm");
rmSync(motelyWasmDst, { recursive: true, force: true });
mkdirSync(motelyWasmDst, { recursive: true });
cpSync(motelyWasmSrc, motelyWasmDst, { recursive: true });

console.log("→ Syncing bundled schema from motely-wasm…");
cpSync(join(motelyWasmSrc, "jaml.schema.json"), schemaDst);

console.log("✓ vendor/jaml-lsp-server ready");
