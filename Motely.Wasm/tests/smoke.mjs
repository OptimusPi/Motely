// End-to-end smoke: serve the AppBundle, load it in headless Chromium, and assert the page's
// own verdict (index.html stamps document.title MOTELY-WASM-OK / MOTELY-WASM-FAIL).
// Usage: node smoke.mjs <appbundle-dir>
import { spawn } from "node:child_process";
import { existsSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { chromium } from "playwright-core";

const bundle = process.argv[2];
const port = 8123;

function findChromium() {
  const roots = [process.env.PLAYWRIGHT_BROWSERS_PATH ?? "/opt/pw-browsers"];
  for (const root of roots) {
    if (!existsSync(root)) continue;
    for (const dir of readdirSync(root)) {
      for (const candidate of [
        join(root, dir, "chrome-linux", "chrome"),
        join(root, dir, "chrome-linux", "headless_shell"),
        join(root, dir, "chrome-headless-shell-linux64", "chrome-headless-shell"),
      ])
        if (existsSync(candidate)) return candidate;
    }
  }
  throw new Error("no chromium found under /opt/pw-browsers");
}

const server = spawn(process.execPath, [join(import.meta.dirname, "serve.mjs"), bundle, String(port)], {
  stdio: "inherit",
});

try {
  const browser = await chromium.launch({
    executablePath: findChromium(),
    args: ["--no-sandbox"],
  });
  const page = await browser.newPage();
  page.on("console", (msg) => console.log("[page]", msg.text()));
  await page.goto(`http://127.0.0.1:${port}/index.html`);
  await page.waitForFunction(() => document.title.startsWith("MOTELY-WASM"), null, {
    timeout: 120_000,
  });
  const title = await page.title();
  const body = await page.locator("#out").textContent();
  console.log(body);
  await browser.close();
  if (title !== "MOTELY-WASM-OK") {
    console.error(`SMOKE FAIL: title=${title}`);
    process.exit(1);
  }
  console.log("SMOKE PASS");
} finally {
  server.kill();
}
