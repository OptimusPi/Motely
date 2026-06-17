// JAML language service — diagnostics / completions / hover / symbols.
// Hand-written logic over the GENERATED tables (generated.ts). The shapes
// returned here are already LSP-shaped; jaml-lsp/src/server.ts only forwards.

import { parseDocument, isMap, isSeq, isScalar, isPair } from "yaml";
import type { Document, Node, Pair, YAMLMap, YAMLSeq, Scalar } from "yaml";
import {
    Enums,
    ClauseKeys,
    SourceKeys,
    DefaultsKeys,
    RootKeys,
    RootValueEnums,
    type KeyInfo,
} from "./generated.js";

// ── LSP shapes ──────────────────────────────────────────────────────────────

export interface Position {
    line: number;
    character: number;
}
export interface Range {
    start: Position;
    end: Position;
}
export interface JamlDiagnostic {
    range: Range;
    message: string;
    severity: 1 | 2 | 3 | 4;
    source?: string;
    code?: string;
}
export type CompletionKind = "keyword" | "enum" | "field" | "value";
export interface JamlCompletion {
    label: string;
    kind: CompletionKind;
    detail?: string;
    documentation?: string;
    insertText?: string;
}
export interface JamlHover {
    contents: string; // markdown
    range?: Range;
}
export interface DocumentSymbol {
    name: string;
    detail?: string;
    kind: "field" | "array" | "object";
    range: Range;
    selectionRange: Range;
    children?: DocumentSymbol[];
}

// ── Offset ↔ position ───────────────────────────────────────────────────────

class LineIndex {
    private readonly starts: number[] = [0];
    constructor(private readonly text: string) {
        for (let i = 0; i < text.length; i++)
            if (text[i] === "\n") this.starts.push(i + 1);
    }
    position(offset: number): Position {
        let lo = 0,
            hi = this.starts.length - 1;
        while (lo < hi) {
            const mid = (lo + hi + 1) >> 1;
            if (this.starts[mid] <= offset) lo = mid;
            else hi = mid - 1;
        }
        return { line: lo, character: offset - this.starts[lo] };
    }
    range(start: number, end: number): Range {
        return { start: this.position(start), end: this.position(end) };
    }
}

// ── Key tables ──────────────────────────────────────────────────────────────

const clauseKeyMap = new Map(ClauseKeys.map((k) => [k.key, k]));
const sourceKeyMap = new Map(SourceKeys.map((k) => [k.key, k]));
const defaultsKeyMap = new Map(DefaultsKeys.map((k) => [k.key, k]));
const rootKeySet = new Set(RootKeys);
const enumMemberIndex = new Map<string, string[]>(); // lowercased member → enum names
for (const [name, members] of Object.entries(Enums)) {
    for (const m of members) {
        const k = m.toLowerCase();
        const list = enumMemberIndex.get(k) ?? [];
        list.push(name);
        enumMemberIndex.set(k, list);
    }
}

function describe(info: KeyInfo): string {
    const s = info.shape;
    if (s.enum)
        return `${s.list ? "list of " : ""}${s.enum}${s.any ? ' (or "Any")' : ""}`;
    return s.type ?? "object";
}

function validValue(info: KeyInfo, raw: string): boolean {
    const s = info.shape;
    if (!s.enum) return true; // non-enum shapes: YAML/loader handle it
    const lower = raw.toLowerCase();
    if (s.any && lower === "any") return true;
    return (Enums[s.enum] ?? []).some((m) => m.toLowerCase() === lower);
}

// ── Diagnostics ─────────────────────────────────────────────────────────────

export function getDiagnostics(text: string): JamlDiagnostic[] {
    const lines = new LineIndex(text);
    const out: JamlDiagnostic[] = [];
    const doc = parseDocument(text);

    for (const err of doc.errors) {
        const [start, end] = err.pos;
        out.push({
            range: lines.range(start, Math.max(end, start + 1)),
            message: err.message.split("\n")[0],
            severity: 1,
            source: "jaml",
            code: err.code,
        });
    }

    const root = doc.contents;
    if (!isMap(root)) return out;

    const report = (node: Node | null | undefined, message: string, code: string) => {
        const [start, end] = (node?.range ?? [0, 1]) as [number, number, number];
        out.push({ range: lines.range(start, end), message, severity: 1, source: "jaml", code });
    };

    const checkValue = (info: KeyInfo, value: Node | null) => {
        if (!info.shape.enum || value == null) return;
        const scalars: Scalar[] = isScalar(value)
            ? [value as Scalar]
            : isSeq(value)
              ? ((value as YAMLSeq).items.filter(isScalar) as Scalar[])
              : [];
        for (const s of scalars) {
            const raw = String(s.value ?? "");
            if (raw && !validValue(info, raw))
                report(s, `'${raw}' is not a ${info.shape.enum} value.`, "bad-enum-value");
        }
    };

    const checkClauseMap = (map: YAMLMap, keyMap: Map<string, KeyInfo>, where: string) => {
        for (const item of map.items) {
            if (!isPair(item) || !isScalar(item.key)) continue;
            const pair = item as Pair<Scalar, Node>;
            const key = String(pair.key.value);
            if (key === "and" || key === "or" || key === "clauses") {
                visitClauseContainer(pair.value);
                continue;
            }
            if (key === "sources" && isMap(pair.value)) {
                checkClauseMap(pair.value as YAMLMap, sourceKeyMap, "a sources block");
                continue;
            }
            const info = keyMap.get(key);
            if (!info) {
                report(pair.key, `Unknown property '${key}' in ${where}.`, "unknown-key");
                continue;
            }
            checkValue(info, pair.value);
        }
    };

    const visitClauseContainer = (node: Node | null | undefined) => {
        if (isSeq(node)) {
            for (const item of (node as YAMLSeq).items)
                if (isMap(item)) checkClauseMap(item as YAMLMap, clauseKeyMap, "a clause");
        } else if (isMap(node)) {
            // legacy nested logic block: { clauses: [...], shared keys }
            checkClauseMap(node as YAMLMap, clauseKeyMap, "a logic block");
        }
    };

    for (const item of root.items) {
        if (!isPair(item) || !isScalar(item.key)) continue;
        const pair = item as Pair<Scalar, Node>;
        const key = String(pair.key.value);
        if (!rootKeySet.has(key)) {
            report(pair.key, `Unknown property '${key}' in the top-level JAML document.`, "unknown-key");
            continue;
        }
        if (key === "must" || key === "should" || key === "mustNot")
            visitClauseContainer(pair.value);
        else if (key === "defaults" && isMap(pair.value))
            checkClauseMap(pair.value as YAMLMap, defaultsKeyMap, "the defaults block");
        else if (key in RootValueEnums && isScalar(pair.value)) {
            const raw = String((pair.value as Scalar).value ?? "");
            const enumName = RootValueEnums[key];
            if (raw && !(Enums[enumName] ?? []).some((m) => m.toLowerCase() === raw.toLowerCase()))
                report(pair.value, `'${raw}' is not a ${enumName} value.`, "bad-enum-value");
        }
    }

    return out;
}

// ── Completions ─────────────────────────────────────────────────────────────

export function getCompletions(text: string, offset: number): JamlCompletion[] {
    const lineStart = text.lastIndexOf("\n", offset - 1) + 1;
    const before = text.slice(lineStart, offset);

    // value position: `key: <partial>`
    const valueCtx = before.match(/^\s*(?:-\s*)?([A-Za-z_]\w*):\s+(\S*)$/);
    if (valueCtx) {
        const key = valueCtx[1];
        const enumName =
            RootValueEnums[key] ??
            clauseKeyMap.get(key)?.shape.enum ??
            sourceKeyMap.get(key)?.shape.enum;
        if (!enumName) return [];
        const values = [...(Enums[enumName] ?? [])];
        if (clauseKeyMap.get(key)?.shape.any) values.push("Any");
        return values.map((v) => ({ label: v, kind: "enum", detail: enumName }));
    }

    // key position: start of line / after `- `
    const keyCtx = before.match(/^(\s*)(?:-\s*)?([A-Za-z_]\w*)?$/);
    if (!keyCtx) return [];
    const indented = keyCtx[1].length > 0;
    if (!indented)
        return RootKeys.map((k) => ({
            label: k,
            kind: "keyword" as const,
            insertText: `${k}: `,
        }));

    const items: JamlCompletion[] = [];
    for (const k of ClauseKeys)
        items.push({ label: k.key, kind: "field", detail: describe(k), insertText: `${k.key}: ` });
    for (const k of SourceKeys)
        items.push({ label: k.key, kind: "field", detail: `sources: ${describe(k)}`, insertText: `${k.key}: ` });
    for (const k of ["and", "or", "clauses", "sources"])
        items.push({ label: k, kind: "keyword", insertText: `${k}:` });
    return items;
}

// ── Hover ───────────────────────────────────────────────────────────────────

export function getHover(text: string, offset: number): JamlHover | null {
    const lines = new LineIndex(text);
    let start = offset,
        end = offset;
    while (start > 0 && /[\w]/.test(text[start - 1])) start--;
    while (end < text.length && /[\w]/.test(text[end])) end++;
    if (start === end) return null;
    const word = text.slice(start, end);
    const range = lines.range(start, end);

    const isKey = text.slice(end).match(/^\s*:/);
    if (isKey) {
        const info = clauseKeyMap.get(word) ?? sourceKeyMap.get(word) ?? defaultsKeyMap.get(word);
        if (info)
            return { contents: `**${word}** — ${describe(info)} (\`${info.csType}\`)`, range };
        if (rootKeySet.has(word)) return { contents: `**${word}** — top-level JAML key`, range };
        return null;
    }

    const owners = enumMemberIndex.get(word.toLowerCase());
    if (owners) return { contents: `**${word}** — ${owners.join(", ")} member`, range };
    return null;
}

// ── Document symbols ────────────────────────────────────────────────────────

function nodeRange(lines: LineIndex, node: Node): Range {
    const [start, , end] = node.range as [number, number, number];
    return lines.range(start, end);
}

export function getDocumentSymbols(text: string): DocumentSymbol[] {
    const lines = new LineIndex(text);
    const doc: Document = parseDocument(text);
    const root = doc.contents;
    if (!isMap(root)) return [];

    const clauseName = (map: YAMLMap): string => {
        for (const item of map.items) {
            if (!isPair(item) || !isScalar(item.key)) continue;
            const key = String((item.key as Scalar).value);
            if (key === "and" || key === "or") return key;
            if (clauseKeyMap.get(key)?.shape.enum || key === "standardCard" || key === "event") {
                const v = isScalar(item.value) ? String((item.value as Scalar).value) : "";
                return v ? `${key}: ${v}` : key;
            }
        }
        return "clause";
    };

    const out: DocumentSymbol[] = [];
    for (const item of root.items) {
        if (!isPair(item) || !isScalar(item.key)) continue;
        const pair = item as Pair<Scalar, Node>;
        const key = String(pair.key.value);
        const keyRange = nodeRange(lines, pair.key);
        const fullRange = pair.value ? nodeRange(lines, pair.value) : keyRange;
        const range = { start: keyRange.start, end: fullRange.end };

        if ((key === "must" || key === "should" || key === "mustNot") && isSeq(pair.value)) {
            const children: DocumentSymbol[] = [];
            for (const clause of (pair.value as YAMLSeq).items) {
                if (!isMap(clause)) continue;
                const r = nodeRange(lines, clause as YAMLMap);
                children.push({
                    name: clauseName(clause as YAMLMap),
                    kind: "object",
                    range: r,
                    selectionRange: r,
                });
            }
            out.push({ name: key, kind: "array", range, selectionRange: keyRange, children });
        } else {
            const detail = isScalar(pair.value) ? String((pair.value as Scalar).value ?? "") : undefined;
            out.push({ name: key, detail, kind: "field", range, selectionRange: keyRange });
        }
    }
    return out;
}

export function validateNames(text: string): { ok: boolean; diagnostics: JamlDiagnostic[] } {
    const diagnostics = getDiagnostics(text);
    return { ok: diagnostics.length === 0, diagnostics };
}
