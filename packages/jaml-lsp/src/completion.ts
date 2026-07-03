import YAML, { Scalar, YAMLMap, YAMLSeq } from "yaml";
import type { CompletionItem } from "vscode-languageserver";
import { CompletionItemKind } from "vscode-languageserver";
import { allKeys, clauseKeys, keyDocs, logicKeys, rootKeys, sharedClauseKeys } from "./keys.js";
import { kindForKey, type Vocabulary } from "./vocab.js";

export interface CursorContext {
  node: unknown;
  path: (string | number)[];
  inKey: boolean;
}

function contains(range: { [0]: number; [1]: number; [2]: number } | null | undefined, offset: number): boolean {
  if (!range) return false;
  return offset >= range[0] && offset <= range[1];
}

export function findContext(
  node: unknown,
  offset: number,
  path: (string | number)[] = [],
): CursorContext | null {
  const n = node as { range?: { [0]: number; [1]: number; [2]: number } };
  if (!contains(n.range, offset)) {
    return null;
  }

  if (node instanceof Scalar) {
    return { node, path, inKey: false };
  }

  if (node instanceof YAMLMap) {
    for (const pair of node.items) {
      const keyNode = pair.key as { range?: { [0]: number; [1]: number; [2]: number } } | null;
      const valueNode = pair.value as unknown;

      if (keyNode && contains(keyNode.range, offset)) {
        return { node: pair.key, path, inKey: true };
      }

      const keyName = (pair.key as Scalar)?.value;
      if (valueNode instanceof YAMLMap || valueNode instanceof YAMLSeq || valueNode instanceof Scalar) {
        const inner = findContext(valueNode, offset, [...path, keyName as string | number]);
        if (inner) return inner;
      }
    }
    return { node, path, inKey: false };
  }

  if (node instanceof YAMLSeq) {
    for (let i = 0; i < node.items.length; i++) {
      const item = node.items[i];
      if (item instanceof YAMLMap || item instanceof YAMLSeq || item instanceof Scalar) {
        const inner = findContext(item, offset, [...path, i]);
        if (inner) return inner;
      }
    }
    return { node, path, inKey: false };
  }

  return { node, path, inKey: false };
}

function last<T>(arr: T[]): T | undefined {
  return arr[arr.length - 1];
}

function isClauseContainer(key: unknown): boolean {
  return key === "must" || key === "should" || key === "mustNot";
}

function toPropertyItem(label: string): CompletionItem {
  return {
    label,
    kind: CompletionItemKind.Property,
    documentation: keyDocs[label],
  };
}

function itemsForContext(ctx: CursorContext, vocab: Vocabulary): CompletionItem[] {
  const { path, inKey } = ctx;
  const lastKey = last(path);

  // Value position after an item key -> offer the engine vocabulary for its kind.
  const kind = kindForKey(lastKey);
  if (!inKey && kind && vocab[kind]) {
    return vocab[kind].map((label) => ({ label, kind: CompletionItemKind.Value }));
  }

  if (!inKey && lastKey === "stickers") {
    return ["Eternal", "Perishable", "Rental"].map((label) => ({
      label,
      kind: CompletionItemKind.Value,
    }));
  }

  // Inside a clause list or nested logic map -> clause/logic/shared keys.
  if (
    inKey &&
    (isClauseContainer(lastKey) || lastKey === "clauses" || lastKey === "and" || lastKey === "or")
  ) {
    return [...clauseKeys, ...sharedClauseKeys, ...logicKeys].map(toPropertyItem);
  }

  // Inside any clause mapping that isn't a special value position.
  if (inKey && path.some((p) => isClauseContainer(p) || p === "clauses" || p === "and" || p === "or")) {
    return [...clauseKeys, ...sharedClauseKeys, ...logicKeys].map(toPropertyItem);
  }

  // Root mapping.
  if (inKey && path.length === 0) {
    return rootKeys.map(toPropertyItem);
  }

  // Fallback: offer every known key.
  return allKeys.map(toPropertyItem);
}

export function getCompletions(
  document: string,
  offset: number,
  vocab: Vocabulary,
): CompletionItem[] {
  let doc;
  try {
    doc = YAML.parseDocument(document, { lineCounter: new YAML.LineCounter() });
  } catch {
    return allKeys.map(toPropertyItem);
  }

  const contents = doc.contents as unknown;
  if (!contents || !(contents instanceof YAMLMap || contents instanceof YAMLSeq || contents instanceof Scalar)) {
    return allKeys.map(toPropertyItem);
  }

  const ctx = findContext(contents, offset);
  if (!ctx) {
    return allKeys.map(toPropertyItem);
  }

  return itemsForContext(ctx, vocab);
}
