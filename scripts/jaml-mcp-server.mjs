#!/usr/bin/env node
/**
 * jaml-lang MCP server — LSP-like JAML authoring tools over stdio.
 *
 * Exposes the jaml-lang brain (validator / completions / hover / context /
 * generated vocab) as MCP tools so coding agents can author *real* JAML:
 * validate before writing, complete from the true Motely vocab, and read
 * hover docs instead of guessing field names.
 *
 * Transport: newline-delimited JSON-RPC 2.0 on stdin/stdout (MCP stdio).
 * No dependencies beyond jaml-lang itself.
 */

import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import readline from "node:readline";

import { getDiagnostics, Severity, getCompletions, getHover, getContext, Vocab } from "jaml-lang";

const PKG = JSON.parse(
  readFileSync(
    join(dirname(fileURLToPath(import.meta.url)), "../node_modules/jaml-lang/package.json"),
    "utf8"
  )
);

const SERVER_INFO = { name: "jaml-lang", version: PKG.version };
const PROTOCOL_VERSION = "2024-11-05";

/* ── helpers ─────────────────────────────────────────────────────────── */

function positionToOffset(text, line, character) {
  const lines = text.split("\n");
  let offset = 0;
  for (let i = 0; i < Math.min(line, lines.length); i++) offset += lines[i].length + 1;
  return Math.min(offset + character, text.length);
}

function resolveOffset(args) {
  const text = String(args.text ?? "");
  if (typeof args.offset === "number") return Math.max(0, Math.min(args.offset, text.length));
  return positionToOffset(text, Number(args.line ?? 0), Number(args.character ?? 0));
}

function textResult(text) {
  return { content: [{ type: "text", text }] };
}

/* ── tools ───────────────────────────────────────────────────────────── */

const TOOLS = [
  {
    name: "jaml_validate",
    description:
      "Validate JAML text and return LSP diagnostics (0-based line/character, severity, message). ALWAYS validate JAML before presenting it to the user.",
    inputSchema: {
      type: "object",
      properties: {
        text: { type: "string", description: "The full JAML document." },
      },
      required: ["text"],
    },
    call(args) {
      const diags = getDiagnostics(String(args.text ?? ""));
      if (diags.length === 0) return textResult("valid — no diagnostics");
      const sev = { [Severity.Error]: "error", [Severity.Warning]: "warning", [Severity.Information]: "info", [Severity.Hint]: "hint" };
      const lines = diags.map(
        (d) => `${sev[d.severity] ?? d.severity} ${d.range.start.line + 1}:${d.range.start.character + 1} — ${d.message}`
      );
      return textResult(lines.join("\n"));
    },
  },
  {
    name: "jaml_completions",
    description:
      "Context-aware completions at a cursor position — the real Motely vocabulary (jokers, vouchers, tags, bosses, decks, stakes, fields, keywords). Use this to learn what is valid instead of inventing names.",
    inputSchema: {
      type: "object",
      properties: {
        text: { type: "string", description: "The full JAML document." },
        line: { type: "number", description: "0-based cursor line (ignored when offset is given)." },
        character: { type: "number", description: "0-based cursor column (ignored when offset is given)." },
        offset: { type: "number", description: "Absolute character offset into text." },
      },
      required: ["text"],
    },
    call(args) {
      const text = String(args.text ?? "");
      const items = getCompletions(text, resolveOffset(args));
      if (items.length === 0) return textResult("no completions at this position");
      const lines = items.map((c) => {
        const doc = c.documentation ? ` — ${c.documentation}` : c.detail ? ` — ${c.detail}` : "";
        const ins = c.insertText && c.insertText !== c.label ? ` (insert: ${c.insertText})` : "";
        return `[${c.kind}] ${c.label}${ins}${doc}`;
      });
      return textResult(lines.join("\n"));
    },
  },
  {
    name: "jaml_hover",
    description: "Hover documentation for the JAML element at a cursor position.",
    inputSchema: {
      type: "object",
      properties: {
        text: { type: "string", description: "The full JAML document." },
        line: { type: "number", description: "0-based cursor line (ignored when offset is given)." },
        character: { type: "number", description: "0-based cursor column (ignored when offset is given)." },
        offset: { type: "number", description: "Absolute character offset into text." },
      },
      required: ["text"],
    },
    call(args) {
      const text = String(args.text ?? "");
      const hover = getHover(text, resolveOffset(args));
      return textResult(hover ? hover.markdown : "no hover info at this position");
    },
  },
  {
    name: "jaml_context",
    description:
      "Classify the editing context at a cursor position (root-key, clause-value, discriminator, …) with the active discriminator, prefix, and value key. Useful before asking for completions.",
    inputSchema: {
      type: "object",
      properties: {
        text: { type: "string", description: "The full JAML document." },
        line: { type: "number", description: "0-based cursor line (ignored when offset is given)." },
        character: { type: "number", description: "0-based cursor column (ignored when offset is given)." },
        offset: { type: "number", description: "Absolute character offset into text." },
      },
      required: ["text"],
    },
    call(args) {
      const text = String(args.text ?? "");
      return textResult(JSON.stringify(getContext(text, resolveOffset(args)), null, 2));
    },
  },
  {
    name: "jaml_vocab",
    description:
      "List the real JAML vocabulary. Without arguments: the grammar shape (root keys, discriminators, enum groups). With `kind`: every valid value for that enum (e.g. joker, voucher, tag, boss, deck, stake).",
    inputSchema: {
      type: "object",
      properties: {
        kind: { type: "string", description: "Enum group name, e.g. 'joker', 'voucher', 'tag', 'boss', 'deck', 'stake'. Omit for an overview." },
      },
    },
    call(args) {
      const kind = args.kind ? String(args.kind) : null;
      if (kind) {
        const hit = Object.entries(Vocab.Enums).find(
          ([k]) => k.toLowerCase() === kind.toLowerCase()
        );
        if (!hit) {
          return textResult(
            `unknown vocab kind "${kind}". available: ${Object.keys(Vocab.Enums).join(", ")}`
          );
        }
        return textResult(`${hit[0]} (${hit[1].length}):\n${hit[1].join("\n")}`);
      }
      const overview = {
        rootKeys: Vocab.RootKeys,
        discriminators: Vocab.Discriminators,
        enumGroups: Object.fromEntries(
          Object.entries(Vocab.Enums).map(([k, v]) => [k, v.length])
        ),
      };
      return textResult(JSON.stringify(overview, null, 2));
    },
  },
];

const TOOL_BY_NAME = new Map(TOOLS.map((t) => [t.name, t]));

/* ── JSON-RPC plumbing ───────────────────────────────────────────────── */

function send(message) {
  process.stdout.write(JSON.stringify(message) + "\n");
}

function respond(id, result) {
  send({ jsonrpc: "2.0", id, result });
}

function respondError(id, code, message) {
  send({ jsonrpc: "2.0", id, error: { code, message } });
}

function handle(message) {
  const { id, method, params } = message;
  const isNotification = id === undefined || id === null;

  try {
    switch (method) {
      case "initialize":
        respond(id, {
          protocolVersion: params?.protocolVersion ?? PROTOCOL_VERSION,
          capabilities: { tools: { listChanged: false } },
          serverInfo: SERVER_INFO,
        });
        return;
      case "ping":
        respond(id, {});
        return;
      case "tools/list":
        respond(id, {
          tools: TOOLS.map(({ name, description, inputSchema }) => ({ name, description, inputSchema })),
        });
        return;
      case "tools/call": {
        const tool = TOOL_BY_NAME.get(params?.name ?? "");
        if (!tool) {
          respondError(id, -32602, `unknown tool: ${params?.name}`);
          return;
        }
        respond(id, tool.call(params?.arguments ?? {}));
        return;
      }
      default:
        if (!isNotification) respondError(id, -32601, `method not found: ${method}`);
    }
  } catch (error) {
    if (!isNotification) {
      respond(id, {
        content: [{ type: "text", text: `tool error: ${error instanceof Error ? error.message : String(error)}` }],
        isError: true,
      });
    }
  }
}

const rl = readline.createInterface({ input: process.stdin, terminal: false });
rl.on("line", (line) => {
  const trimmed = line.trim();
  if (!trimmed) return;
  let message;
  try {
    message = JSON.parse(trimmed);
  } catch {
    respondError(null, -32700, "parse error");
    return;
  }
  handle(message);
});
