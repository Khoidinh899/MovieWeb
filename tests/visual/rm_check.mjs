import { chromium, webkit } from 'playwright';
for (const [name, engine] of [['chromium', chromium], ['webkit', webkit]]) {
  const b = await (name==='chromium' ? engine.launch({channel:'msedge'}) : engine.launch());
  const ctx = await b.newContext({ viewport:{width:1440,height:900}, reducedMotion:'reduce' });
  await ctx.addCookies([{ name:'moonphim_ui', value:'v2', domain:'localhost', path:'/' }]);
  const p = await ctx.newPage();
  await p.goto('http://localhost:5001/Auth/Login',{waitUntil:'networkidle'});
  await p.evaluate(async ({email,password})=>{const t=document.querySelector('input[name="__RequestVerificationToken"]')?.value||document.getElementById('RequestVerificationToken')?.value;await fetch('/Auth/Login',{method:'POST',body:new URLSearchParams({Email:email,Password:password,RememberMe:'true',__RequestVerificationToken:t||''}),headers:{'X-Requested-With':'XMLHttpRequest','RequestVerificationToken':t||''}})},{email:process.env.V2_TEST_EMAIL,password:process.env.V2_TEST_PASSWORD});
  await p.goto('http://localhost:5001/trang-chu',{waitUntil:'domcontentloaded'}); await p.waitForTimeout(2500);
  const r = {};
  r.rmMatches = await p.evaluate(()=>matchMedia('(prefers-reduced-motion: reduce)').matches);
  r.staggerGroups = await p.evaluate(()=>document.querySelectorAll('.v2-stagger').length);
  r.heroTextTransition = await p.evaluate(()=>getComputedStyle(document.querySelector('.home-hero-text')).transitionDuration);
  const btn = p.locator('.home-hero-slide.is-active .v2-btn-3d--primary').first();
  await btn.hover(); await p.waitForTimeout(150);
  r.shimmer = await btn.evaluate(e=>getComputedStyle(e,'::after').animationName);
  r.btnTransition = await btn.evaluate(e=>getComputedStyle(e).transitionDuration);
  await p.mouse.move(2,2);
  await p.evaluate(()=>window.scrollTo(0,1500)); await p.waitForTimeout(100);
  r.staggerAfterScroll = await p.evaluate(()=>document.querySelectorAll('.v2-stagger.is-staggered, .v2-stagger').length);
  // popup
  await p.goto('http://localhost:5001/phim/ong-bean',{waitUntil:'domcontentloaded'}); await p.waitForTimeout(2500);
  const shown = await p.evaluate(()=>{ const t=window.watchProgressTracker; if(!t) return 'no tracker'; t.showResumePopup({episodeNumber:3, serverName:'vietsub #1', watchedDuration:120}); return 'ok'; });
  r.popup = shown;
  await p.waitForTimeout(500);
  r.popupAnim = await p.evaluate(()=>{const c=document.querySelector('.resume-content'); return c&&getComputedStyle(c).animationName});
  await p.screenshot({ path:`out/p5/resume-popup-${name}.png` });
  console.log(name, JSON.stringify(r));
  await b.close();
}
