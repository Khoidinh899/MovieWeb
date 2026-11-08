import { chromium, webkit } from 'playwright';
const base='http://localhost:5001';
for (const [name, engine] of [['chromium', chromium], ['webkit', webkit]]) {
  const b = await (name==='chromium' ? engine.launch({channel:'msedge'}) : engine.launch());
  const ctx = await b.newContext({ viewport:{width:390,height:844}, isMobile:true, hasTouch:true });
  await ctx.addCookies([{ name:'moonphim_ui', value:'v2', domain:'localhost', path:'/' }]);
  const p = await ctx.newPage();
  await p.goto(base+'/Auth/Login',{waitUntil:'networkidle'});
  await p.evaluate(async ({email,password})=>{const t=document.querySelector('input[name="__RequestVerificationToken"]')?.value||document.getElementById('RequestVerificationToken')?.value;await fetch('/Auth/Login',{method:'POST',body:new URLSearchParams({Email:email,Password:password,RememberMe:'true',__RequestVerificationToken:t||''}),headers:{'X-Requested-With':'XMLHttpRequest','RequestVerificationToken':t||''}})},{email:process.env.V2_TEST_EMAIL,password:process.env.V2_TEST_PASSWORD});
  await p.goto(base+'/trang-chu',{waitUntil:'domcontentloaded'}); await p.waitForTimeout(2500);
  const st = () => p.evaluate(()=>({open:document.getElementById('v2Sidebar').classList.contains('is-open'), avatarVisible: !!document.getElementById('userDropdown') && getComputedStyle(document.getElementById('userDropdown').parentElement).display!=='none', burger: getComputedStyle(document.getElementById('v2MenuOpen')).display}));
  console.log(name,'initial',JSON.stringify(await st()));
  await p.click('#v2MenuOpen'); await p.waitForTimeout(500);
  console.log(name,'after open',JSON.stringify(await st()));
  await p.click('[data-v2-sidebar-toggle="v2SideCats"]'); await p.waitForTimeout(300);
  await p.screenshot({path:`out/p5/sidebar-${name}.png`});
  const cats = await p.evaluate(()=>document.querySelectorAll('#v2SideCats a').length);
  await p.click('.v2-sidebar-backdrop',{position:{x:370,y:400}}); await p.waitForTimeout(500);
  console.log(name,'after backdrop close',JSON.stringify(await st()),'cats=',cats);
  await p.click('#v2MenuOpen'); await p.waitForTimeout(400);
  await Promise.all([p.waitForURL(/the-loai\/phim-le/,{timeout:15000}), p.click('#v2Sidebar a[href="/the-loai/phim-le"]')]);
  console.log(name,'nav ok',p.url());
  await b.close();
}
