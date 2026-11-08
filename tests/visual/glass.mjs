import { chromium, webkit } from 'playwright';
for (const [name, engine] of [['chromium', chromium], ['webkit', webkit]]) {
  const b = await (name==='chromium' ? engine.launch({channel:'msedge'}) : engine.launch());
  const ctx = await b.newContext({ viewport:{width:390,height:844}, isMobile:true, hasTouch:true });
  await ctx.addCookies([{ name:'moonphim_ui', value:'v2', domain:'localhost', path:'/' }]);
  const p = await ctx.newPage();
  await p.goto('http://localhost:5001/Auth/Login',{waitUntil:'networkidle'});
  await p.evaluate(async ({email,password})=>{const t=document.querySelector('input[name="__RequestVerificationToken"]')?.value||document.getElementById('RequestVerificationToken')?.value;await fetch('/Auth/Login',{method:'POST',body:new URLSearchParams({Email:email,Password:password,RememberMe:'true',__RequestVerificationToken:t||''}),headers:{'X-Requested-With':'XMLHttpRequest','RequestVerificationToken':t||''}})},{email:process.env.V2_TEST_EMAIL,password:process.env.V2_TEST_PASSWORD});
  await p.goto('http://localhost:5001/trang-chu',{waitUntil:'domcontentloaded'}); await p.waitForTimeout(2500);
  await p.click('#v2SearchOpen'); await p.waitForTimeout(500);
  const bell = await p.evaluate(()=>getComputedStyle(document.querySelector('.notification-wrapper')).visibility);
  await p.screenshot({path:`out/p5/search-overlay-${name}.png`, clip:{x:0,y:0,width:390,height:300}});
  await p.click('#v2SearchClose'); await p.waitForTimeout(300);
  const bell2 = await p.evaluate(()=>getComputedStyle(document.querySelector('.notification-wrapper')).visibility);
  await p.click('#v2MenuOpen'); await p.waitForTimeout(600);
  const bell3 = await p.evaluate(()=>getComputedStyle(document.querySelector('.notification-wrapper')).visibility);
  await p.screenshot({path:`out/p5/sidebar-glass-${name}.png`});
  console.log(name,'bell in overlay:',bell,'after close:',bell2,'in sidebar:',bell3);
  await b.close();
}
