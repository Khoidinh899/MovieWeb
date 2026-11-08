// Chụp nhanh các trang V2 (cần đăng nhập) ở desktop/mobile, chromium + webkit. Chỉ dùng tài khoản test LOCAL.
// Dùng: V2_TEST_EMAIL=... V2_TEST_PASSWORD=... node shots.mjs <base> <outDir> <path1> [path2 ...]
import { chromium, webkit } from 'playwright';
import fs from 'node:fs';

const [base, outDir, ...paths] = process.argv.slice(2);
const email = process.env.V2_TEST_EMAIL, password = process.env.V2_TEST_PASSWORD;
fs.mkdirSync(outDir, { recursive: true });
const hostname = new URL(base).hostname;
const VPS = { desktop: { width: 1440, height: 900 }, mobile: { width: 390, height: 844 } };

for (const [engName, engine] of [['chromium', chromium], ['webkit', webkit]]) {
  const browser = await (engName === 'chromium' ? engine.launch({ channel: 'msedge' }) : engine.launch());
  for (const [vpName, vp] of Object.entries(VPS)) {
    const ctx = await browser.newContext({ viewport: vp, isMobile: vp.width < 768, hasTouch: vp.width < 768 });
    await ctx.addCookies([{ name: 'moonphim_ui', value: 'v2', domain: hostname, path: '/' }]);
    const page = await ctx.newPage();
    const errors = [];
    page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 160)); });
    page.on('pageerror', e => errors.push('pageerror: ' + String(e).slice(0, 160)));
    if (email) {
      await page.goto(base + '/Auth/Login', { waitUntil: 'networkidle' });
      await page.evaluate(async ({ email, password }) => {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value
          || document.getElementById('RequestVerificationToken')?.value;
        const body = new URLSearchParams({ Email: email, Password: password, RememberMe: 'true', __RequestVerificationToken: token || '' });
        await fetch('/Auth/Login', { method: 'POST', body, headers: { 'X-Requested-With': 'XMLHttpRequest', 'RequestVerificationToken': token || '' } });
      }, { email, password });
    }
    for (const p of paths) {
      errors.length = 0;
      await page.goto(base + p, { waitUntil: 'networkidle', timeout: 60000 }).catch(() => {});
      await page.waitForTimeout(1200);
      const name = `${p.replace(/[^a-z0-9]+/gi, '_').replace(/^_|_$/g, '') || 'root'}-${vpName}-${engName}`;
      await page.screenshot({ path: `${outDir}/${name}.png`, fullPage: true });
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1);
      console.log(`${name}: title="${await page.title()}" overflowX=${overflow} errors=${errors.length}${errors.length ? ' ' + JSON.stringify(errors.slice(0, 3)) : ''}`);
    }
    await ctx.close();
  }
  await browser.close();
}
