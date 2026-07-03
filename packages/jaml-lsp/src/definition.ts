import path from "node:path";
import { pathToFileURL } from "node:url";
import type { Definition, DefinitionLink, LocationLink } from "vscode-languageserver";
import { keyDocs } from "./keys.js";

const here = path.dirname(new URL(import.meta.url).pathname);
const schemaPath = path.join(here, "..", "docs", "schema.md");

export function getDefinition(word: string): Definition | DefinitionLink[] | null {
  if (!keyDocs[word]) {
    return null;
  }

  const targetUri = pathToFileURL(schemaPath).toString();
  const link: LocationLink = {
    targetUri,
    targetRange: { start: { line: 0, character: 0 }, end: { line: 0, character: 0 } },
    targetSelectionRange: { start: { line: 0, character: 0 }, end: { line: 0, character: 0 } },
  };
  return [link];
}
