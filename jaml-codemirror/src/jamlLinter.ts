import type { Diagnostic } from "@codemirror/lint";
import { validate } from "jaml-lang";
import { MotelyJaml } from "motely-wasm";
import YAML, { Scalar, YAMLMap, YAMLSeq } from "yaml";

// jaml-lang's validate() already returns character-offset diagnostics in
// exactly CodeMirror's { from, to, severity, message } shape — no adapter
// needed beyond the severity string, which matches too. This is the same
// vocabulary the VS Code extension and LSP server validate against, straight
// from Motely.Schema.cs, so there is nothing here to drift out of sync.

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

/**
 * Lints JAML source for CodeMirror. Combines jaml-lang's structural
 * validation (root/discriminator/clause keys, enum values) with the engine's
 * own MotelyJaml.validate/validateLine, which catches whole-document and
 * JUMMY one-line clause errors jaml-lang's lightweight walker doesn't.
 */
export async function jamlLinter(source: string): Promise<Diagnostic[]> {
  const diagnostics: Diagnostic[] = validate(source).map((d) => ({
    from: d.from,
    to: d.to,
    severity: d.severity,
    message: d.message,
  }));

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
