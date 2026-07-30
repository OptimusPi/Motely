import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import pkg from "../package.json" with { type: "json" };

mkdirSync("dist/ui", { recursive: true });
writeFileSync(
  "dist/ui/jimbo.css",
  readFileSync("src/ui/jimbo.css", "utf8").replace(
    /__JAML_UI_VERSION__/g,
    pkg.version
  )
);
