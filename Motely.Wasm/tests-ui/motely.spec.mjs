import { test, expect } from "@playwright/test";

// The product, proven where UX lives: real Chromium, the shipped module, and the CM6
// editor — zero buttons for validation, ever.
test.beforeEach(async ({ page }) => {
  await page.goto("/testui/index.html");
  await expect(page.locator("#status")).toContainText("booted", { timeout: 60_000 });
});

test("boots and reports the engine version", async ({ page }) => {
  await expect(page.locator("#status")).toContainText(/booted · motely \d+\./);
});

test("live lint validates while typing — good, structurally bad, and vocabulary typos", async ({ page }) => {
  await expect(page.locator("#validation")).toHaveText("valid ✓", { timeout: 15_000 });

  await page.evaluate(() => __motely.setDoc("must: ["));
  await expect(page.locator("#validation")).toHaveClass(/error/, { timeout: 15_000 });

  // A typo'd joker validates structurally but can never match — the vocabulary lint flags it.
  await page.evaluate(() => __motely.setDoc("name: t\ndeck: Red\nstake: White\nmust:\n  - joker: LuckyCatz\n"));
  await expect(page.locator(".cm-lintRange-error").first()).toBeVisible({ timeout: 15_000 });
});

test("completion offers LuckyCat before the word is finished", async ({ page }) => {
  await page.evaluate(() => __motely.setDoc("name: t\ndeck: Red\nstake: White\nmust:\n  - joker: "));
  await page.locator(".cm-content").click();
  await page.keyboard.press("Control+End");
  await page.keyboard.type("luckyc", { delay: 40 });
  await expect(page.locator(".cm-tooltip-autocomplete")).toContainText("LuckyCat", { timeout: 15_000 });
});

test("search returns scored seeds and streams progress", async ({ page }) => {
  await page.click("#search");
  const rows = page.locator("#results tr");
  await expect(rows).toHaveCount(2, { timeout: 60_000 });
  await expect(rows.first().locator("td").first()).toHaveText(/^[1-9A-Z]{8}$/);
  await expect(page.locator("#progress")).toContainText("searched 2");
});
