import { MotelyJaml } from "motely-wasm";
import type { Diagnostic } from "vscode-languageserver";
import { DiagnosticSeverity } from "vscode-languageserver";
import type { TextDocument } from "vscode-languageserver-textdocument";
import YAML, { type Document, type Range, Scalar, YAMLMap, YAMLSeq } from "yaml";

function rangeStart(range: Range | null | undefined): number {
  return range?.[0] ?? 0;
}

function rangeToPosition(doc: Document, range: Range | null | undefined) {
  const offset = rangeStart(range);
  const lineCounter = (doc as unknown as { lineCounter?: { linePos: (n: number) => { line: number; col: number } } }).lineCounter;
  if (!lineCounter) {
    return { line: 0, character: 0 };
  }
  const pos = lineCounter.linePos(offset);
  return { line: pos.line - 1, character: pos.col - 1 };
}

function isJummyCandidate(value: unknown): value is string {
  return typeof value === "string" && value.trim().length > 0;
}

function looksLikeJummyLine(line: string): boolean {
  // JUMMY lines are plain scalars inside clause lists; mapping entries are key: value.
  const beforeQuote = line.split(/["']/)[0];
  if (/^\s*[\w-]+\s*:\s+/.test(beforeQuote)) {
    return false;
  }
  return true;
}

function collectJummyScalars(node: unknown, out: Scalar<string>[], parentIsSeq = false) {
  if (node instanceof Scalar && typeof node.value === "string") {
    if (parentIsSeq) {
      out.push(node as Scalar<string>);
    }
    return;
  }
  if (node instanceof YAMLSeq) {
    for (const item of node.items) {
      if (item) collectJummyScalars(item as unknown, out, true);
    }
  }
  if (node instanceof YAMLMap) {
    for (const pair of node.items) {
      if (pair.value instanceof YAMLMap || pair.value instanceof YAMLSeq || pair.value instanceof Scalar) {
        collectJummyScalars(pair.value, out, false);
      }
    }
  }
}

export function validateDocument(document: TextDocument): Diagnostic[] {
  const text = document.getText();
  const diagnostics: Diagnostic[] = [];

  const wholeDocError = MotelyJaml.validate(text);
  if (wholeDocError) {
    diagnostics.push({
      severity: DiagnosticSeverity.Error,
      range: {
        start: { line: 0, character: 0 },
        end: { line: 0, character: 0 },
      },
      message: wholeDocError,
      source: "jaml",
    });
  }

  let doc: Document;
  try {
    doc = YAML.parseDocument(text, { lineCounter: new YAML.LineCounter() });
  } catch {
    return diagnostics;
  }

  const scalars: Scalar<string>[] = [];
  if (doc.contents instanceof YAMLMap || doc.contents instanceof YAMLSeq || doc.contents instanceof Scalar) {
    collectJummyScalars(doc.contents, scalars);
  }

  for (const scalar of scalars) {
    const line = scalar.value;
    if (!isJummyCandidate(line) || !looksLikeJummyLine(line)) {
      continue;
    }
    const error = MotelyJaml.validateLine(line);
    if (error) {
      const start = rangeToPosition(doc, scalar.range);
      diagnostics.push({
        severity: DiagnosticSeverity.Error,
        range: {
          start,
          end: { line: start.line, character: start.character + line.length },
        },
        message: `JUMMY: ${error}`,
        source: "jaml",
      });
    }
  }

  return diagnostics;
}
