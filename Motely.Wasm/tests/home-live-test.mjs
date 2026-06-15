// Live test: drive the real Motely.Home app's worker and run a real search.
import { chromium } from "playwright";

const BASE = "http://127.0.0.1:5179";

let browser = null;
for (const channel of ["msedge", "chrome", undefined]) {
    try {
        browser = await chromium.launch(channel ? { channel } : {});
        break;
    } catch {}
}
if (!browser) {
    console.log("HOME LIVE: FAIL — no browser");
    process.exit(1);
}

const page = await browser.newPage();
const errors = [];
page.on("console", (m) => { if (m.type() === "error") errors.push(m.text()); });
page.on("pageerror", (e) => errors.push(String(e)));

await page.goto(`${BASE}/app`);

const messages = await page.evaluate(async () => {
    const msgs = [];
    const worker = new Worker("/worker.mjs", { type: "module" });
    const finished = new Promise((resolve) => {
        worker.onmessage = ({ data }) => {
            msgs.push(data);
            if (["done", "error", "parseError"].includes(data.type)) resolve();
        };
        worker.onerror = (e) => {
            msgs.push({ type: "workerError", message: e.message });
            resolve();
        };
    });
    worker.postMessage({
        type: "random",
        jaml: "name: live\nmust:\n  - joker: Blueprint\n    antes: [1]\n",
        count: 5000,
    });
    await Promise.race([finished, new Promise((r) => setTimeout(r, 120_000))]);
    worker.terminate();
    // Strip bulky payloads, keep shape.
    return msgs.map((m) => ({
        type: m.type,
        message: m.message,
        data: m.type === "progress" ? { seedsSearched: String(m.data?.seedsSearched ?? m.data?.totalSeedsSearched ?? "?") } :
              m.type === "result" ? { seed: m.data?.seed } : undefined,
    }));
});

await browser.close();

console.log(JSON.stringify({ messages, errors }, null, 2));
const ok = messages.some((m) => m.type === "done");
console.log(ok ? "HOME LIVE: PASS" : "HOME LIVE: FAIL");
process.exit(ok ? 0 : 1);
