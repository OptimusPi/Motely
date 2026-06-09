// Protocol-level smoke: spawn the built server and drive it over real LSP
// JSON-RPC (Content-Length framing). Proves the wire surface works end-to-end,
// not just that the TypeScript compiles.
//
//   npm run build && npm run smoke

import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import {
  createMessageConnection,
  StreamMessageReader,
  StreamMessageWriter,
} from "vscode-jsonrpc/node.js";

const here = dirname(fileURLToPath(import.meta.url));
const serverPath = join(here, "..", "dist", "server.js");

let failures = 0;
function check(name: string, cond: boolean): void {
  console.log(`  ${cond ? "ok  " : "FAIL"} ${name}`);
  if (!cond) failures++;
}

const child = spawn(process.execPath, [serverPath, "--stdio"], {
  stdio: ["pipe", "pipe", "inherit"],
});

const conn = createMessageConnection(
  new StreamMessageReader(child.stdout),
  new StreamMessageWriter(child.stdin)
);

const diagnostics = new Map<string, any[]>();
conn.onNotification("textDocument/publishDiagnostics", (p: any) => {
  diagnostics.set(p.uri, p.diagnostics);
});

conn.listen();

const URI = "file:///mem/test.jaml";
// A deliberately broken filter: BAD enum value should produce a diagnostic.
const TEXT = ["deck: Red", "must:", "  - joker: NotARealJoker"].join("\n");

function notify(method: string, params: unknown): void {
  void conn.sendNotification(method, params);
}

async function main(): Promise<void> {
  const init: any = await conn.sendRequest("initialize", {
    processId: process.pid,
    rootUri: null,
    capabilities: {},
  });
  check("initialize returns capabilities", !!init?.capabilities);
  check("declares completionProvider", !!init.capabilities.completionProvider);
  check("declares hoverProvider", init.capabilities.hoverProvider === true);
  check(
    "declares documentSymbolProvider",
    init.capabilities.documentSymbolProvider === true
  );

  notify("initialized", {});
  notify("textDocument/didOpen", {
    textDocument: { uri: URI, languageId: "jaml", version: 1, text: TEXT },
  });

  // Give the server a tick to publish diagnostics.
  await new Promise((r) => setTimeout(r, 200));
  const diags = diagnostics.get(URI) ?? [];
  check("publishes a diagnostic for the bad enum", diags.length > 0);

  // Completion at the `deck:` value position (line 0, after "deck: ").
  const comp: any = await conn.sendRequest("textDocument/completion", {
    textDocument: { uri: URI },
    position: { line: 0, character: 9 },
  });
  const items = Array.isArray(comp) ? comp : comp?.items ?? [];
  check("completion returns items", items.length > 0);

  // Document symbols: must clause should appear in the outline.
  const syms: any = await conn.sendRequest("textDocument/documentSymbol", {
    textDocument: { uri: URI },
  });
  check(
    "document symbols include 'must'",
    Array.isArray(syms) && syms.some((s: any) => s.name === "must")
  );

  // Hover on the `joker` key.
  const hover: any = await conn.sendRequest("textDocument/hover", {
    textDocument: { uri: URI },
    position: { line: 2, character: 5 },
  });
  check("hover responds (object or null, no crash)", hover !== undefined);

  await conn.sendRequest("shutdown", null);
  notify("exit", null);
  conn.dispose();
  child.kill();

  console.log(`\nRESULT: ${failures === 0 ? "PASS" : "FAIL"} (${failures} failures)`);
  process.exit(failures === 0 ? 0 : 1);
}

main().catch((e) => {
  console.error(e);
  child.kill();
  process.exit(1);
});
