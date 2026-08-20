// Smoke-drives the Motely.Lsp binary over real stdio: initialize, open a document
// with a typo, expect positioned diagnostics, shut down clean.
// Run: node Motely.Lsp/smoke-lsp.mjs <path-to-Motely.Lsp>
import { spawn } from "node:child_process";

const exe = process.argv[2] ?? process.env.MOTELY_LSP_SERVER;
if (!exe) {
    console.error("usage: node smoke-lsp.mjs <Motely.Lsp-exe>");
    process.exit(2);
}

const server = spawn(exe, [], { stdio: ["pipe", "pipe", "inherit"] });

const frame = (msg) => {
    const payload = JSON.stringify(msg);
    return `Content-Length: ${Buffer.byteLength(payload)}\r\n\r\n${payload}`;
};

let buffer = Buffer.alloc(0);
const messages = [];
server.stdout.on("data", (chunk) => {
    buffer = Buffer.concat([buffer, chunk]);
    while (true) {
        const headerEnd = buffer.indexOf("\r\n\r\n");
        if (headerEnd < 0) return;
        const length = Number(/Content-Length: (\d+)/i.exec(buffer.subarray(0, headerEnd))[1]);
        if (buffer.length < headerEnd + 4 + length) return;
        messages.push(JSON.parse(buffer.subarray(headerEnd + 4, headerEnd + 4 + length)));
        buffer = buffer.subarray(headerEnd + 4 + length);
    }
});

server.stdin.write(frame({ jsonrpc: "2.0", id: 1, method: "initialize", params: {} }));
server.stdin.write(frame({
    jsonrpc: "2.0", method: "textDocument/didOpen",
    params: { textDocument: { uri: "file:///smoke.jaml", languageId: "jaml", version: 1, text: "name: smoke\nboses:\n" } },
}));
server.stdin.write(frame({ jsonrpc: "2.0", id: 2, method: "shutdown" }));
server.stdin.write(frame({ jsonrpc: "2.0", method: "exit" }));

server.on("exit", (code) => {
    const init = messages.find((m) => m.id === 1);
    const diags = messages.find((m) => m.method === "textDocument/publishDiagnostics");
    const ok =
        code === 0 &&
        init?.result?.serverInfo?.name === "Motely.Lsp" &&
        diags?.params.diagnostics.length === 1 &&
        diags.params.diagnostics[0].message.includes("boses") &&
        diags.params.diagnostics[0].range.start.line === 1;
    console.log(ok ? "LSP SMOKE PASS" : "LSP SMOKE FAIL");
    if (!ok) {
        console.log("exit code:", code);
        console.log(JSON.stringify(messages, null, 2));
        process.exitCode = 1;
    }
});
