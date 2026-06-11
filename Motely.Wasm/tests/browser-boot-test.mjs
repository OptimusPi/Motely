#!/usr/bin/env node
/**
 * REAL-browser boot gate (release skill step 3). Serves this directory over
 * http, drives a real browser (Edge, falling back to Chrome) via Playwright,
 * loads browser-boot-test.html, and asserts window.__RESULT.ok.
 *
 *   node Motely.Wasm/tests/browser-boot-test.mjs
 *
 * Exits 0 + "BROWSER BOOT: PASS" on success.
 */
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { dirname, extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";

const root = normalize(join(dirname(fileURLToPath(import.meta.url)), ".."));

const MIME = {
    ".html": "text/html",
    ".mjs": "text/javascript",
    ".js": "text/javascript",
    ".json": "application/json",
    ".wasm": "application/wasm",
    ".dat": "application/octet-stream",
};

const server = createServer(async (req, res) => {
    try {
        const path = normalize(join(root, decodeURIComponent(req.url.split("?")[0])));
        if (!path.startsWith(root)) throw new Error("traversal");
        const body = await readFile(path);
        res.writeHead(200, {
            "content-type": MIME[extname(path)] ?? "application/octet-stream",
        });
        res.end(body);
    } catch {
        res.writeHead(404).end("not found");
    }
});

await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
const port = server.address().port;

const { chromium } = await import("playwright");

let browser = null;
for (const channel of ["msedge", "chrome", undefined]) {
    try {
        browser = await chromium.launch(channel ? { channel } : {});
        console.log(`browser: ${channel ?? "bundled chromium"}`);
        break;
    } catch {
        /* next channel */
    }
}
if (!browser) {
    console.error("BROWSER BOOT: FAIL — no browser available");
    process.exit(1);
}

const page = await browser.newPage();
page.on("console", (msg) => {
    if (msg.type() === "error") console.error("[page]", msg.text());
});

await page.goto(`http://127.0.0.1:${port}/tests/browser-boot-test.html`);
const result = await page.waitForFunction(() => window.__RESULT, null, {
    timeout: 120_000,
});
const value = await result.jsonValue();

await browser.close();
server.close();

console.log(JSON.stringify(value, null, 2));
const ok = value?.ok === true;
console.log(ok ? "BROWSER BOOT: PASS" : "BROWSER BOOT: FAIL");
process.exit(ok ? 0 : 1);
