import { MotelyJaml } from "motely-wasm";
import type { Diagnostic } from "@codemirror/lint";
import YAML, { Scalar, YAMLMap, YAMLSeq } from "yaml";

function collectSeqStringScalars(node: unknown, out: Scalar<string>[], parentIsSeq = false) {
  if (node instanceof Scalar && typeof node.value === "string") {
    if (parentIsSeq) out.push(node as Scalar<string>);
    return;
  }
  if (node instanceof YAMLSeq) {
    for (const item of node.items) {
      if (item) collectSeqStringScalars(item as unknown, out, true);
    }
  }
  if (node instanceof YAMLMap) {
    for (const pair of node.items) {
      const value = pair.value as unknown;
      if (value instanceof YAMLMap || value instanceof YAMLSeq || value instanceof Scalar) {
        collectSeqStringScalars(value, out, false);
      }
    }
  }
}

function looksLikeJummyLine(line: string): boolean {
  const beforeQuote = line.split(/["']/)[0];
  if (/^\s*[\w-]+\s*:\s+/.test(beforeQuote)) {
    return false;
  }
  return true;
}

function rangeToPos(doc: YAML.Document, range: { [0]: number; [1]: number; [2]: number }) {
  const lc = (doc as unknown as { lineCounter?: YAML.LineCounter }).lineCounter;
  if (!lc) return { line: 0, col: 0 };
  return lc.linePos(range[0]);
}

export async function jamlLinter(source: string): Promise<Diagnostic[]> {
  const diagnostics: Diagnostic[] = [];

  const wholeDocError = MotelyJaml.validate(source);
  if (wholeDocError) {
    diagnostics.push({
      from: 0,
      to: source.length,
      severity: "error",
      message: wholeDocError,
    });
  }

  let doc: YAML.Document;
  try {
    doc = YAML.parseDocument(source, { lineCounter: new YAML.LineCounter() });
  } catch {
    return diagnostics;
  }

  const scalars: Scalar<string>[] = [];
  const contents = doc.contents as unknown;
  if (contents instanceof YAMLMap || contents instanceof YAMLSeq || contents instanceof Scalar) {
    collectSeqStringScalars(contents, scalars);
  }

  for (const scalar of scalars) {
    const line = scalar.value;
    if (!line.trim() || !looksLikeJummyLine(line)) continue;
    const error = MotelyJaml.validateLine(line);
    if (error && scalar.range) {
      const from = scalar.range[0];
      diagnostics.push({
        from,
        to: from + line.length,
        severity: "error",
        message: `JUMMY: ${error}`,
      });
    }
  }

  return diagnostics;
}
