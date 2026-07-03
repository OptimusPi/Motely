import YAML, { Scalar, YAMLMap, YAMLSeq } from "yaml";
import type { Hover } from "vscode-languageserver";
import { findContext } from "./completion.js";
import { keyDocs } from "./keys.js";

export function getHover(document: string, offset: number): Hover | null {
  let doc;
  try {
    doc = YAML.parseDocument(document, { lineCounter: new YAML.LineCounter() });
  } catch {
    return null;
  }

  const contents = doc.contents as unknown;
  if (!contents || !(contents instanceof YAMLMap || contents instanceof YAMLSeq || contents instanceof Scalar)) {
    return null;
  }

  const ctx = findContext(contents, offset);
  if (!ctx) return null;

  if (ctx.inKey && ctx.node instanceof Scalar && typeof ctx.node.value === "string") {
    const docs = keyDocs[ctx.node.value];
    if (docs) {
      return {
        contents: {
          kind: "markdown",
          value: `**${ctx.node.value}**\n\n${docs}`,
        },
      };
    }
  }

  if (!ctx.inKey && ctx.node instanceof Scalar && typeof ctx.node.value === "string") {
    return {
      contents: {
        kind: "markdown",
        value: `JUMMY line: \`${ctx.node.value}\``,
      },
    };
  }

  return null;
}
