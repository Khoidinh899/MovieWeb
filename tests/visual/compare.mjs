// Side-by-side visual comparison: Stitch design (left) vs V2 page (right), same viewport.
// Usage:  node compare.mjs [baseUrl] [name-filter]
//         e.g. node compare.mjs http://localhost:5001 home
// Output: tests/visual/out/<name>.png (+ raw stitch/v2 shots). Needs the app running in Development.
import { chromium } from 'playwright';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');
const base = process.argv[2]?.startsWith('http') ? process.argv[2] : 'http://localhost:5001';
const filter = process.argv.find((a, i) => i >= 2 && !a.startsWith('http')) || '';
const out = path.join(here, 'out');
fs.mkdirSync(out, { recursive: true });

const S1 = 'stitch_moonphim_cinematic_streaming_ui01';
const S2 = 'stitch_moonphim_cinematic_streaming_ui02';
const DESKTOP = { width: 1440, height: 900 };
const MOBILE = { width: 390, height: 844 };

// name, Stitch screen folder, V2 path, viewport, needsLogin
const PAIRS = [
  ['home-desktop', `${S2}/moonphim_trang_ch_desktop_tinh_ch_nh_phim_trending`, '/trang-chu', DESKTOP],
  ['home-mobile', `${S2}/moonphim_trang_ch_mobile_tinh_ch_nh_phim_trending`, '/trang-chu', MOBILE],
  ['list-desktop', `${S2}/moonphim_danh_m_c_phim_b_l_c_a_n_ng_desktop`, '/the-loai/phim-le', DESKTOP],
  ['list-mobile', `${S2}/moonphim_danh_m_c_kh_m_ph_phim_mobile`, '/the-loai/phim-le', MOBILE],
  ['filter-desktop', `${S1}/moonphim_ph_n_lo_i_b_l_c_phim_chuy_n_s_u_desktop`, '/the-loai/phim-bo', DESKTOP],
  ['filter-mobile', `${S1}/moonphim_danh_m_c_th_lo_i_b_l_c_di_ng_mobile`, '/the-loai/phim-bo', MOBILE],
  ['search-desktop', `${S2}/moonphim_k_t_qu_t_m_ki_m_i_n_nh_desktop`, '/tim-kiem?q=bean', DESKTOP],
  ['search-mobile', `${S2}/moonphim_k_t_qu_t_m_ki_m_mobile`, '/tim-kiem?q=bean', MOBILE],
  ['detail-desktop', `${S2}/moonphim_chi_ti_t_phim_rift_saigon_2099_desktop`, '/phim/ong-bean', DESKTOP],
  ['detail-mobile', `${S2}/moonphim_chi_ti_t_phim_mobile_rift_saigon_2099`, '/phim/ong-bean', MOBILE],
  ['player-desktop', `${S2}/moonphim_tr_nh_ph_t_video_desktop_rift_saigon_2099`, '/phim/ong-bean/tap-full?server=vietsub-1', DESKTOP],
  ['player-mobile', `${S2}/moonphim_tr_nh_ph_t_video_mobile_rift_saigon_2099`, '/phim/ong-bean/tap-full?server=vietsub-1', MOBILE],
  ['login-desktop', `${S1}/moonphim_ng_nh_p_ng_k_desktop`, '/Auth/Login', DESKTOP],
  ['login-mobile', `${S1}/moonphim_ng_nh_p_ng_k_mobile`, '/Auth/Login', MOBILE],
  ['about-desktop', `${S1}/moonphim_trung_t_m_th_ng_tin_gi_i_thi_u_faq_desktop`, '/about', DESKTOP],
  ['about-mobile', `${S1}/moonphim_th_ng_tin_gi_i_thi_u_h_tr_faq_mobile`, '/about', MOBILE],
  ['dmca-desktop', `${S1}/moonphim_ch_nh_s_ch_dmca_i_u_kho_n_b_n_quy_n_desktop`, '/dmca', DESKTOP],
  ['profile-desktop', `${S1}/moonphim_h_s_c_nh_n_ti_n_ch_desktop`, '/user/profile', DESKTOP, true],
  ['profile-mobile', `${S1}/moonphim_h_s_c_nh_n_c_i_t_mobile`, '/user/profile', MOBILE, true],
  ['security-desktop', `${S1}/moonphim_c_i_t_t_i_kho_n_b_o_m_t_desktop`, '/user/edit', DESKTOP, true],
  ['security-mobile', `${S1}/moonphim_c_i_t_t_i_kho_n_b_o_m_t_mobile`, '/user/edit', MOBILE, true],
  ['history-desktop', `${S1}/moonphim_qu_n_l_l_ch_s_xem_phim_l_u_desktop`, '/user/history', DESKTOP, true],
  ['history-mobile', `${S1}/moonphim_danh_s_ch_y_u_th_ch_l_ch_s_xem_mobile`, '/user/history', MOBILE, true],
  ['notifications-desktop', `${S1}/moonphim_trung_t_m_th_ng_b_o_t_ng_t_c_desktop`, '/user/notifications', DESKTOP, true],
  ['notifications-mobile', `${S1}/moonphim_trung_t_m_th_ng_b_o_mobile`, '/user/notifications', MOBILE, true],
].map(([name, stitch, url, vp, needsLogin]) => ({ name, stitch, url, vp, needsLogin: !!needsLogin }));

// Optional login for user pages: set V2_TEST_EMAIL / V2_TEST_PASSWORD (a LOCAL test account only).
const email = process.env.V2_TEST_EMAIL, password = process.env.V2_TEST_PASSWORD;

// Prefer the system Edge (always on Windows); fall back to Playwright's Chromium.
const browser = await chromium.launch({ channel: 'msedge' }).catch(() => chromium.launch());
const hostname = new URL(base).hostname;

async function newContext(vp) {
  const ctx = await browser.newContext({ viewport: vp, deviceScaleFactor: 1, isMobile: vp.width < 768, hasTouch: vp.width < 768 });
  await ctx.addCookies([{ name: 'moonphim_ui', value: 'v2', domain: hostname, path: '/' }]);
  return ctx;
}

async function login(ctx) {
  const page = await ctx.newPage();
  await page.goto(base + '/Auth/Login', { waitUntil: 'networkidle' });
  const res = await page.evaluate(async ({ email, password }) => {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value
      || document.getElementById('RequestVerificationToken')?.value;
    const body = new URLSearchParams({ Email: email, Password: password, RememberMe: 'true', __RequestVerificationToken: token || '' });
    const r = await fetch('/Auth/Login', { method: 'POST', body, headers: { 'X-Requested-With': 'XMLHttpRequest', 'RequestVerificationToken': token || '' } });
    return r.status;
  }, { email, password });
  await page.close();
  return res;
}

async function shot(page, url, file) {
  await page.goto(url, { waitUntil: 'networkidle', timeout: 60000 }).catch(() => {});
  await page.waitForTimeout(1500);
  await page.screenshot({ path: file, fullPage: true });
}

// about:blank cannot load file:// images, so embed them.
const dataUri = f => 'data:image/png;base64,' + fs.readFileSync(f).toString('base64');

const rows = [];
for (const p of PAIRS.filter(p => p.name.includes(filter))) {
  const stitchFile = path.join(repo, p.stitch, 'code.html');
  if (!fs.existsSync(stitchFile)) { console.log(`[skip] ${p.name}: missing ${p.stitch}`); continue; }
  const ctx = await newContext(p.vp);
  if (p.needsLogin) {
    if (!email) { console.log(`[skip] ${p.name}: needs login (set V2_TEST_EMAIL/V2_TEST_PASSWORD)`); await ctx.close(); continue; }
    await login(ctx);
  }
  const page = await ctx.newPage();
  const a = path.join(out, `${p.name}.stitch.png`), b = path.join(out, `${p.name}.v2.png`);
  await shot(page, pathToFileURL(stitchFile).href, a);
  await shot(page, base + p.url, b);
  const finalUrl = page.url();
  await ctx.close();

  // Compose side by side with a plain HTML page (no image library needed).
  const comp = await browser.newPage({ viewport: { width: p.vp.width * 2 + 48, height: 800 } });
  await comp.setContent(`<html><body style="margin:0;background:#222;font:14px sans-serif;color:#fff">
    <div style="display:flex;gap:16px;padding:16px;align-items:flex-start">
      <div><div style="padding:4px 0">STITCH — ${p.stitch.split('/').pop()}</div><img src="${dataUri(a)}" style="width:${p.vp.width}px;display:block"></div>
      <div><div style="padding:4px 0">V2 — ${finalUrl.replace(base, '')}</div><img src="${dataUri(b)}" style="width:${p.vp.width}px;display:block"></div>
    </div></body></html>`, { waitUntil: 'load' });
  const file = path.join(out, `${p.name}.png`);
  await comp.screenshot({ path: file, fullPage: true });
  await comp.close();
  rows.push(`${p.name}: ${path.relative(repo, file)}${finalUrl.includes('/Auth/Login') && p.url !== '/Auth/Login' ? '  (redirected to login)' : ''}`);
  console.log(`[ok] ${p.name}`);
}
await browser.close();
fs.writeFileSync(path.join(out, 'INDEX.txt'), rows.join('\n') + '\n');
console.log(`\n${rows.length} comparisons in ${path.relative(repo, out)}`);
