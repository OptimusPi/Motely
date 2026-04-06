/**
 * Copies repo-root jaml.schema.json into this package (single pipeline; no hand edits).
 */
import { copyFileSync, existsSync } from "fs";
import { dirname, resolve } from "path";
import { fileURLToPath } from "url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, "../../..");
const src = resolve(repoRoot, "jaml.schema.json");
const dst = resolve(__dirname, "jaml.schema.json");

if (!existsSync(src)) {
  if (existsSync(dst)) {
    // Running from a flattened publish copy — schema already present, nothing to do.
    process.exit(0);
  }
  throw new Error(`sync-schema: missing ${src} (run dotnet CLI --write-jaml-schema from repo root).`);
}
copyFileSync(src, dst);
