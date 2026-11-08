// Kiểm tra Đợt 5: vuốt banner (mobile), nút lên đầu trang, chatbot, nút 3D ở trang chi tiết/danh sách. Tài khoản test LOCAL.
// Dùng: V2_TEST_EMAIL=... V2_TEST_PASSWORD=... node phase5.mjs <base> <outDir>
import { chromium, webkit } from 'playwright';
import fs from 'node:fs';

const [base, outDir] = process.argv.slice(2);
fs.mkdirSync(outDir, { recursive: true });
const email = process.env.V2_TEST_EMAIL, password = process.env.V2_TEST_PASSWORD;
const host = new URL(base).hostname;

for (const [engName, engine] of [['chromium', chromium], ['webkit', webkit]]) {
  const browser = await (engName === 'chromium' ? engine.launch({ channel: 'msedge' }) : engine.launch());
  const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
  await ctx.addCookies([{ name: 'moonphim_ui', value: 'v2', domain: host, path: '/' }]);
  const p = await ctx.newPage();
  const errs = [];
  p.on('pageerror', e => { const m = String(e); if (!m.includes('rtmark')) errs.push(m.slice(0, 140)); });
  await p.goto(base + '/Auth/Login', { waitUntil: 'networkidle' });
  await p.evaluate(async ({ email, password }) => {
    const t = document.querySelector('input[name="__RequestVerificationToken"]')?.value || document.getElementById('RequestVerificationToken')?.value;
    await fetch('/Auth/Login', { method: 'POST', body: new URLSearchParams({ Email: email, Password: password, RememberMe: 'true', __RequestVerificationToken: t || '' }), headers: { 'X-Requested-With': 'XMLHttpRequest', 'RequestVerificationToken': t || '' } });
  }, { email, password });

  // --- vuốt banner
  await p.goto(base + '/trang-chu', { waitUntil: 'domcontentloaded' });
  await p.waitForTimeout(2500);
  const counter = () => p.evaluate(() => document.getElementById('heroCounter')?.textContent);
  const c0 = await counter();
  const swipe = async (dx) => {
    const box = await p.locator('#heroSlider').boundingBox();
    const y = box.y + Math.min(box.height / 2, 300), x0 = box.x + box.width / 2;
    const cdp = ctx.newCDPSession ? await ctx.newCDPSession(p).catch(() => null) : null;
    if (cdp) {
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: x0, y }] });
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: x0 + dx / 2, y }] });
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: x0, y }] });
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    } else {
      await p.evaluate(({ x0, y, dx }) => {
        const el = document.getElementById('heroSlider');
        const mk = (type, x) => new TouchEvent(type, { bubbles: true, cancelable: true, changedTouches: [new Touch({ identifier: 1, target: el, clientX: x, clientY: y })], touches: type === 'touchend' ? [] : [new Touch({ identifier: 1, target: el, clientX: x, clientY: y })] });
        el.dispatchEvent(mk('touchstart', x0)); el.dispatchEvent(mk('touchend', x0 + dx));
      }, { x0, y, dx });
    }
  };
  // Dùng sự kiện tổng hợp cho cả 2 engine (WebKit không có CDP)
  const synth = async (dx) => p.evaluate(({ dx }) => {
    const el = document.getElementById('heroSlider'); const r = el.getBoundingClientRect(); const y = r.top + 200, x0 = r.left + r.width / 2;
    const mk = (type, x) => { const ev = new Event(type, { bubbles: true, cancelable: true }); const t = { clientX: x, clientY: y }; ev.touches = type === 'touchend' ? [] : [t]; ev.changedTouches = [t]; return ev; };
    el.dispatchEvent(mk('touchstart', x0)); el.dispatchEvent(mk('touchend', x0 + dx));
  }, { dx });
  await synth(-120); await p.waitForTimeout(300);
  const c1 = await counter();
  await synth(120); await p.waitForTimeout(300);
  const c2 = await counter();
  await synth(10); await p.waitForTimeout(200);
  const c3 = await counter();
  console.log(`${engName} swipe: ${c0} -> (trái) ${c1} -> (phải) ${c2} -> (vuốt nhỏ 10px, không đổi) ${c3}`);

  // --- nút lên đầu trang
  const hiddenTop = await p.evaluate(() => document.getElementById('scrollTopBtn').hidden);
  await p.evaluate(() => window.scrollTo(0, 1200)); await p.waitForTimeout(500);
  const shown = await p.evaluate(() => !document.getElementById('scrollTopBtn').hidden);
  const rect = await p.evaluate(() => { const r = document.getElementById('scrollTopBtn').getBoundingClientRect(); return [Math.round(r.right), Math.round(r.bottom), Math.round(r.width)]; });
  await p.screenshot({ path: `${outDir}/p5-scrolltop-${engName}.png` });
  await p.click('#scrollTopBtn'); await p.waitForTimeout(1200);
  const y = await p.evaluate(() => Math.round(window.scrollY));
  console.log(`${engName} scrollTop: hiddenAtTop=${hiddenTop} shownAfterScroll=${shown} rect(right,bottom,w)=${rect} yAfterClick=${y}`);

  // --- chatbot
  const fab = await p.evaluate(() => { const e = document.getElementById('chatbotFloatingBtn'); if (!e) return null; const r = e.getBoundingClientRect(); return [Math.round(r.right), Math.round(r.bottom), Math.round(r.width)]; });
  let chat = 'không có FAB';
  if (fab) {
    await p.evaluate(() => window.scrollTo(0, 0));
    await p.click('#chatbotFloatingBtn');
    await p.waitForTimeout(600);
    const open = await p.evaluate(() => getComputedStyle(document.getElementById('chatbotPopup')).display);
    await p.fill('#popupMessageInput', 'gợi ý phim hài');
    await p.screenshot({ path: `${outDir}/p5-chatbot-${engName}.png` });
    const reqP = p.waitForResponse(r => r.url().includes('/api/Chatbot/SendMessage'), { timeout: 20000 }).catch(() => null);
    await p.click('#popupSendButton');
    const resp = await reqP;
    await p.waitForTimeout(1500);
    const bubbles = await p.evaluate(() => document.querySelectorAll('#popupMessagesList .message-bubble').length);
    chat = `popupDisplay=${open} fab=${fab} sendStatus=${resp ? resp.status() : 'no-response'} bubbles=${bubbles}`;
  }
  console.log(`${engName} chatbot: ${chat}`);

  // --- nút 3D chi tiết phim
  await p.goto(base + '/phim/ong-bean', { waitUntil: 'domcontentloaded' });
  await p.waitForTimeout(2500);
  const info = await p.evaluate(() => ({
    watch: document.getElementById('watchBtn')?.classList.contains('v2-btn-3d'),
    fav: document.getElementById('favoriteBtn')?.classList.contains('v2-btn-3d'),
    tabs: [...document.querySelectorAll('.server-tab')].every(t => t.classList.contains('v2-btn-3d')),
    activeTab: document.querySelector('.server-tab.active') ? getComputedStyle(document.querySelector('.server-tab.active'), '::after').backgroundColor : null,
    overflowX: document.documentElement.scrollWidth > innerWidth + 1,
  }));
  await p.screenshot({ path: `${outDir}/p5-detail-${engName}.png`, fullPage: true });
  console.log(`${engName} detail:`, JSON.stringify(info), 'errors=', JSON.stringify(errs.slice(0, 3)));
  await ctx.close(); await browser.close();
}
