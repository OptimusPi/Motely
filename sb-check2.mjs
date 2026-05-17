import { chromium } from 'playwright';

const browser = await chromium.launch();
const page = await browser.newPage();
page.setViewportSize({ width: 1400, height: 900 });

const errors = [];
const logs = [];
page.on('console', msg => logs.push(`[${msg.type()}] ${msg.text()}`));
page.on('pageerror', err => errors.push(err.message));

await page.goto('http://localhost:6006/?path=/story/jimboui-components--typography', { waitUntil: 'networkidle', timeout: 30000 });
await page.waitForTimeout(4000);

// Click to dismiss any onboarding overlay
await page.keyboard.press('Escape');
await page.waitForTimeout(1000);

await page.screenshot({ path: 'x:/jaml-ui/sb-screenshot2.png', fullPage: true });

console.log('=== ERRORS ===');
for (const e of errors) console.log(e);
console.log('=== LOGS (errors/warnings only) ===');
for (const l of logs.filter(l => l.includes('[error]'))) console.log(l);

await browser.close();
