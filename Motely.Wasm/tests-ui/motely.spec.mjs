import { test, expect } from "@playwright/test";

// Default testui doc: seeds [AAAAAAAA, BBBBBBBB], should joker:Any score 1.
// Proof = those known seeds appear scored — shape-regex is not proof.
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

// Engine vocab surface the editor completion uses (listItems) — pin LuckyCat, no tooltip flake.
test("listItems offers LuckyCat for joker luckyc", async ({ page }) => {
  const names = await page.evaluate(() => MotelyJaml.listItems("joker", "luckyc"));
  expect(names).toContain("LuckyCat");
});
test("searchList finds default-doc seeds AAAAAAAA and BBBBBBBB", async ({ page }) => {
  await page.click("#search");
  const rows = page.locator("#results tr");
  await expect(rows).toHaveCount(2, { timeout: 60_000 });
  const seeds = await rows.locator("td:first-child").allTextContents();
  expect(seeds.sort()).toEqual(["AAAAAAAA", "BBBBBBBB"]);
  await expect(page.locator("#progress")).toContainText("searched 2");
});
