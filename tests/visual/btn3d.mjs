// Chụp 3 trạng thái (thường / hover / nhấn) của nút 3D. Dùng: node btn3d.mjs <base> <outDir>
import { chromium, webkit } from 'playwright';
import fs from 'node:fs';

const [base, outDir] = process.argv.slice(2);
fs.mkdirSync(outDir, { recursive: true });
const TARGETS = [
  { name: 'hero-watch', url: '/trang-chu', sel: '#heroSlider .home-hero-slide.is-active a[href^="/phim/"]:has-text("Xem Ngay")' },
  { name: 'hero-detail', url: '/trang-chu', sel: '#heroSlider .home-hero-slide.is-active a[href^="/phim/"]:has-text("Chi Tiết")' },
  { name: 'landing-cta', url: '/', sel: '.cta-button' },
  { name: 'player-play', url: '/phim/ong-bean', sel: '#placeholderPlay' },
];
const VPS = { desktop: { width: 1440, height: 900 }, mobile: { width: 390, height: 844 } };

for (const [engName, engine] of [['chromium', chromium], ['webkit', webkit]]) {
  const browser = await (engName === 'chromium' ? engine.launch({ channel: 'msedge' }) : engine.launch());
  for (const [vpName, vp] of Object.entries(VPS)) {
    const ctx = await browser.newContext({ viewport: vp, isMobile: false, hasTouch: false, deviceScaleFactor: 2 });
    await ctx.addCookies([{ name: 'moonphim_ui', value: 'v2', domain: new URL(base).hostname, path: '/' }]);
    const page = await ctx.newPage();
    for (const t of TARGETS) {
      await page.goto(base + t.url, { waitUntil: 'networkidle', timeout: 60000 }).catch(() => {});
      await page.waitForTimeout(1500);
      const el = page.locator(t.sel).first();
      if (!(await el.count())) { console.log(`${t.name}-${vpName}-${engName}: không thấy nút`); continue; }
      await el.scrollIntoViewIfNeeded();
      await page.waitForTimeout(300);
      const box = await el.boundingBox();
      const clip = { x: Math.max(0, box.x - 30), y: Math.max(0, box.y - 30), width: Math.min(vp.width, box.width + 60), height: box.height + 70 };
      const info = {};
      await page.mouse.move(2, 2);
      await page.waitForTimeout(250);
      await page.screenshot({ path: `${outDir}/btn3d-${t.name}-${vpName}-${engName}-1normal.png`, clip });
      info.normal = await el.evaluate(e => getComputedStyle(e).transform);
      await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
      await page.waitForTimeout(300);
      await page.screenshot({ path: `${outDir}/btn3d-${t.name}-${vpName}-${engName}-2hover.png`, clip });
      info.hover = await el.evaluate(e => getComputedStyle(e).transform);
      await page.mouse.down();
      await page.waitForTimeout(300);
      await page.screenshot({ path: `${outDir}/btn3d-${t.name}-${vpName}-${engName}-3active.png`, clip });
      info.active = await el.evaluate(e => getComputedStyle(e).transform);
      await page.mouse.up();
      console.log(`${t.name}-${vpName}-${engName}: ${JSON.stringify(info)} h=${Math.round(box.height)}`);
    }
    await ctx.close();
  }
  await browser.close();
}
