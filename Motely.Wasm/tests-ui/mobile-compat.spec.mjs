import { test, expect } from "@playwright/test";

// The phone reality check. Safari before 18.2 and Chrome before 140 have no
// Uint8Array.fromBase64/fromHex/toBase64 — and bootsharp's config.mjs decodes the embedded
// .NET assemblies with fromBase64 during boot(). Deleting the methods before any script runs
// makes desktop Chromium behave exactly like those phones, so this test failing means
// seedfinder.app renders a blank screen on a 2024 iPhone. The prepended polyfill must carry
// boot all the way through on atob alone — Buffer does not exist in a browser.
test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
        delete Uint8Array.fromBase64;
        delete Uint8Array.fromHex;
        delete Uint8Array.prototype.toBase64;
        delete Uint8Array.prototype.toHex;
        delete Uint8Array.prototype.setFromBase64;
        delete Uint8Array.prototype.setFromHex;
    });
    await page.goto("/testui/index.html");
});

test("boots and searches on a browser without Uint8Array.fromBase64", async ({ page }) => {
    await expect(page.locator("#status")).toContainText("booted", { timeout: 60_000 });

    // Boot alone proves the assembly decode; the default document's search proves the
    // whole engine came up and can score seeds — same flow the desktop spec relies on.
    await page.click("#search");
    await expect(page.locator("#results tr").first()).toBeVisible({ timeout: 15_000 });
});
