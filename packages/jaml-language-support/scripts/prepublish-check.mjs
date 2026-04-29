import { access, readFile } from "node:fs/promises";
import { constants } from "node:fs";

const requiredFiles = [
  "package.json",
  "README.md",
  "language-configuration.json",
  "src/extension.cjs",
  "syntaxes/jaml.tmLanguage.json",
  "syntaxes/jummy.tmLanguage.json",
  "snippets/jaml.code-snippets",
  "schema/jaml.schema.json",
  "images/icon.ico"
];

for (const file of requiredFiles) {
  await access(new URL(`../${file}`, import.meta.url), constants.R_OK);
}

const manifest = JSON.parse(await readFile(new URL("../package.json", import.meta.url), "utf8"));
if (manifest.publisher !== "pifreak" || manifest.name !== "jaml-language-support") {
  throw new Error("Package identity must remain pifreak.jaml-language-support for Marketplace revival.");
}

console.log("jaml-language-support prepublish check ok");
