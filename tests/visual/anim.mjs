// Kiểm tra animation Đợt 5: CLS trang chủ (reduced-motion = đối chứng không hiệu ứng), stagger, hero text, shimmer, dropdown.
// Dùng: node anim.mjs <base>
import { chromium, webkit } from 'playwright';
const base = process.argv[2];

async function cls(page, url) {
  await page.addInitScript(() => {
    window.__cls = 0;
    new PerformanceObserver(l => { for (const e of l.getEntries()) if (!e.hadRecentInput) window.__cls += e.value; }).observe({ type: 'layout-shift', buffered: true });
  });
  await page.goto(base + url, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3000);
  for (let y = 0; y <= 4000; y += 500) { await page.evaluate(y => window.scrollTo(0, y), y); await page.waitForTimeout(250); }
  await page.waitForTimeout(1500);
  return page.evaluate(() => +window.__cls.toFixed(4));
}

for (const [engName, engine] of [['chromium', chromium], ['webkit', webkit]]) {
  const browser = await (engName === 'chromium' ? engine.launch({ channel: 'msedge' }) : engine.launch());
  for (const [vpName, vp, mobile] of [['desktop', { width: 1440, height: 900 }, false], ['mobile', { width: 390, height: 844 }, true]]) {
    const out = {};
    for (const rm of ['reduce', 'no-preference']) {
      const ctx = await browser.newContext({ viewport: vp, isMobile: mobile, hasTouch: mobile, reducedMotion: rm });
      await ctx.addCookies([{ name: 'moonphim_ui', value: 'v2', domain: 'localhost', path: '/' }]);
      const p = await ctx.newPage();
      out['cls_' + rm] = await cls(p, '/trang-chu');
      if (rm === 'no-preference') {
        const state = await p.evaluate(() => ({
          groups: document.querySelectorAll('[data-v2-img-group]').length,
          leftoverStagger: document.querySelectorAll('.v2-stagger').length,
          hiddenCards: [...document.querySelectorAll('.movie-card')].filter(c => getComputedStyle(c).opacity === '0').length,
          heroText: (() => { const a = document.querySelector('.home-hero-slide.is-active .home-hero-text'); const b = document.querySelector('.home-hero-slide:not(.is-active) .home-hero-text'); return [a && getComputedStyle(a).opacity, b && getComputedStyle(b).opacity]; })(),
        }));
        out.state = state;
        // shimmer: hover nút primary hero
        const btn = p.locator('.home-hero-slide.is-active .v2-btn-3d--primary').first();
        if (await btn.count() && !mobile) {
          await btn.hover();
          await p.waitForTimeout(200);
          out.shimmer = await btn.evaluate(e => getComputedStyle(e, '::after').animationName);
        }
      }
      await ctx.close();
    }
    console.log(`${engName} ${vpName}:`, JSON.stringify(out));
  }
  await browser.close();
}
