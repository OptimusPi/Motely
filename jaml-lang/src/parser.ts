import * as yaml from "yaml";

export interface Position {
  line: number;
  char: number;
}

export interface Range {
  start: Position;
  end: Position;
}

export type NodeType = "document" | "mapping" | "sequence" | "scalar" | "error";

export interface JamlNode {
  type: NodeType;
  value: string;
  range: Range;
  children: JamlNode[];
  parent: JamlNode | null;
  key?: JamlNode;
  keyName?: string;
}

function emptyRange(line: number, char: number): Range {
  return { start: { line, char }, end: { line, char } };
}

function makeNode(
  type: NodeType,
  value: string,
  range: Range,
  parent: JamlNode | null = null
): JamlNode {
  return { type, value, range, children: [], parent };
}

function posFromOffset(lineCounter: yaml.LineCounter, offset: number): Position {
  const pos = lineCounter.linePos(offset);
  return { line: pos.line - 1, char: pos.col - 1 };
}

function getRange(node: any, lineCounter: yaml.LineCounter): Range {
  const r = node.range as number[] | undefined;
  if (!r || r.length < 2) return emptyRange(0, 0);
  if (r.length === 3) {
    return {
      start: posFromOffset(lineCounter, r[0]),
      end: posFromOffset(lineCounter, r[2]),
    };
  }
  return {
    start: posFromOffset(lineCounter, r[0]),
    end: posFromOffset(lineCounter, r[1]),
  };
}


function yamlToJamlNode(
  node: unknown,
  lineCounter: yaml.LineCounter,
  parent: JamlNode | null = null
): JamlNode | null {
  if (node === null || node === undefined) return null;

  if (node instanceof yaml.Scalar) {
    const jn = makeNode("scalar", String(node.value ?? ""), getRange(node, lineCounter), parent);
    return jn;
  }

  if (node instanceof yaml.YAMLMap) {
    const mapNode = makeNode("mapping", "", getRange(node, lineCounter), parent);
    for (const pair of node.items) {
      const keyNode = yamlToJamlNode(pair.key, lineCounter, mapNode);
      if (keyNode && keyNode.type === "scalar") {
        mapNode.children.push(keyNode);
        const valNode = yamlToJamlNode(pair.value, lineCounter, mapNode);
        if (valNode) {
          valNode.keyName = keyNode.value;
          mapNode.children.push(valNode);
        }
      }
    }
    return mapNode;
  }

  if (node instanceof yaml.YAMLSeq) {
    const seqNode = makeNode("sequence", "", getRange(node, lineCounter), parent);
    for (const item of node.items) {
      const child = yamlToJamlNode(item, lineCounter, seqNode);
      if (child) seqNode.children.push(child);
    }
    return seqNode;
  }

  if (node instanceof yaml.Pair) {
    // Pair at top level — treat as a mini mapping
    const mapNode = makeNode("mapping", "", getRange(node.key, lineCounter), parent);
    const keyNode = yamlToJamlNode(node.key, lineCounter, mapNode);
    if (keyNode && keyNode.type === "scalar") {
      mapNode.children.push(keyNode);
      const valNode = yamlToJamlNode(node.value, lineCounter, mapNode);
      if (valNode) {
        valNode.keyName = keyNode.value;
        mapNode.children.push(valNode);
      }
    }
    return mapNode;
  }

  // Fallback for plain objects/arrays (shouldn't happen with yaml package)
  return null;
}

export function parseJaml(text: string): JamlNode {
  const lineCounter = new yaml.LineCounter();
  const doc = yaml.parseDocument(text, { lineCounter });

  const root = makeNode("document", "", emptyRange(0, 0), null);

  if (doc.contents) {
    const child = yamlToJamlNode(doc.contents, lineCounter, root);
    if (child) {
      root.children.push(child);
      root.range.end = child.range.end;
    }
  }

  return root;
}

// ── Helpers for the service layer ───────────────────────────────────────────

export function findNodeAtPosition(
  doc: JamlNode,
  line: number,
  char: number
): JamlNode | null {
  function walk(node: JamlNode): JamlNode | null {
    const { start, end } = node.range;
    if (
      line < start.line ||
      (line === start.line && char < start.char) ||
      line > end.line ||
      (line === end.line && char > end.char)
    ) {
      return null;
    }
    for (const child of node.children) {
      const found = walk(child);
      if (found) return found;
    }
    return node;
  }
  return walk(doc);
}

export function findKeyParent(node: JamlNode): JamlNode | null {
  let cur = node.parent;
  while (cur) {
    if (cur.type === "mapping" && cur.parent?.type === "sequence") {
      return cur;
    }
    cur = cur.parent;
  }
  return null;
}

export function findSectionKey(node: JamlNode): string | null {
  let cur = node.parent;
  while (cur) {
    if (cur.keyName && ["must", "should", "mustNot"].includes(cur.keyName)) {
      return cur.keyName;
    }
    cur = cur.parent;
  }
  return null;
}

export function getLineText(text: string, line: number): string {
  return text.split(/\r?\n/)[line] ?? "";
}

export function getWordAtPosition(text: string, line: number, char: number): string {
  const lineText = getLineText(text, line);
  let start = char;
  let end = char;
  while (start > 0 && /[\w-]/.test(lineText[start - 1])) start--;
  while (end < lineText.length && /[\w-]/.test(lineText[end])) end++;
  return lineText.slice(start, end);
}

export function getLinePrefix(text: string, line: number, char: number): string {
  const lineText = getLineText(text, line);
  return lineText.slice(0, char);
}
