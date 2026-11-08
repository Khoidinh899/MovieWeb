// Test Watch Party V2: tạo phòng, 2 ngữ cảnh cùng vào, chat realtime. Chỉ tài khoản test LOCAL.
// Dùng: V2_TEST_EMAIL=... V2_TEST_PASSWORD=... node watchparty.mjs <base> <outDir>
import { chromium, webkit } from 'playwright';
import fs from 'node:fs';

const [base, outDir] = process.argv.slice(2);
fs.mkdirSync(outDir, { recursive: true });
const email = process.env.V2_TEST_EMAIL, password = process.env.V2_TEST_PASSWORD;

async function login(ctx) {
  await ctx.addCookies([{ name: 'moonphim_ui', value: 'v2', domain: new URL(base).hostname, path: '/' }]);
  const page = await ctx.newPage();
  await page.goto(base + '/Auth/Login', { waitUntil: 'networkidle' });
  await page.evaluate(async ({ email, password }) => {
    const t = document.querySelector('input[name="__RequestVerificationToken"]')?.value || document.getElementById('RequestVerificationToken')?.value;
    await fetch('/Auth/Login', { method: 'POST', body: new URLSearchParams({ Email: email, Password: password, RememberMe: 'true', __RequestVerificationToken: t || '' }), headers: { 'X-Requested-With': 'XMLHttpRequest', 'RequestVerificationToken': t || '' } });
  }, { email, password });
  return page;
}

for (const [engName, engine] of [['chromium', chromium], ['webkit', webkit]]) {
  const browser = await (engName === 'chromium' ? engine.launch({ channel: 'msedge' }) : engine.launch());
  const errs = [];
  const ctxA = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const a = await login(ctxA);
  a.on('pageerror', e => errs.push('A ' + String(e).slice(0, 140)));
  await a.goto(base + '/watch-party', { waitUntil: 'networkidle' });
  await a.screenshot({ path: `${outDir}/index-desktop-${engName}.png`, fullPage: true });
  await a.click('#wpBtnOpenCreate');
  await a.fill('#wpMovieSearch', 'bean');
  await a.waitForSelector('#wpMovieSearchResults > *', { timeout: 15000 });
  await a.click('#wpMovieSearchResults > *');
  await a.waitForTimeout(1500);
  await a.fill('#wpRoomTitleInput', 'Phong test V2 ' + engName);
  await a.screenshot({ path: `${outDir}/create-modal-${engName}.png` });
  await Promise.all([a.waitForURL(/watch-party\/[A-Za-z0-9]+/, { timeout: 20000 }), a.click('#wpCreateRoomModal button[type=submit]')]);
  const roomUrl = a.url();
  await a.waitForTimeout(2500);
  await a.screenshot({ path: `${outDir}/room-desktop-${engName}.png`, fullPage: true });

  const ctxB = await browser.newContext({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
  const b = await login(ctxB);
  b.on('pageerror', e => errs.push('B ' + String(e).slice(0, 140)));
  await b.goto(roomUrl, { waitUntil: 'networkidle' });
  await b.waitForTimeout(2500);
  await b.screenshot({ path: `${outDir}/room-mobile-${engName}.png`, fullPage: true });
  const overflow = await b.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1);

  const msg = 'xin chao ' + Date.now();
  await a.fill('#wpChatInput', msg);
  await a.click('#wpBtnSendChat');
  await b.waitForTimeout(2000);
  const gotB = await b.evaluate(m => document.body.innerText.includes(m), msg);
  const gotA = await a.evaluate(m => document.body.innerText.includes(m), msg);
  console.log(`${engName}: room=${roomUrl} chat A->B delivered=${gotB} selfEcho=${gotA} mobileOverflowX=${overflow} errors=${JSON.stringify(errs)}`);
  await browser.close();
}
