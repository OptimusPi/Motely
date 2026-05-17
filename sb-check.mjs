import { chromium } from 'playwright';

const browser = await chromium.launch();
const page = await browser.newPage();

const errors = [];
const logs = [];

page.on('console', msg => logs.push(`[${msg.type()}] ${msg.text()}`));
page.on('pageerror', err => errors.push(err.message));

await page.goto('http://localhost:6006/?path=/story/jaml-jamlide--default', { waitUntil: 'networkidle', timeout: 30000 });
await page.waitForTimeout(5000);

await page.screenshot({ path: 'x:/jaml-ui/sb-screenshot.png', fullPage: true });

console.log('=== CONSOLE LOGS ===');
for (const l of logs) console.log(l);
console.log('=== PAGE ERRORS ===');
for (const e of errors) console.log(e);

await browser.close();
