import { mkdirSync, copyFileSync, existsSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("..", import.meta.url));
const dist = resolve(root, "dist");
const repoJamlSchema = resolve(root, "..", "..", "..", "jaml.schema.json");

mkdirSync(resolve(dist, "app"), { recursive: true });
copyFileSync(resolve(root, "src/app/view.html"), resolve(dist, "app/view.html"));
if (existsSync(repoJamlSchema)) {
  copyFileSync(repoJamlSchema, resolve(dist, "jaml.schema.json"));
}
