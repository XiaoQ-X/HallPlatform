const fs=require('fs'),path=require('path'),assert=require('node:assert/strict');const {chromium}=require('C:/Users/a19098/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
fs.mkdirSync(path.resolve(__dirname,'../work'),{recursive:true});process.env.HALL_DATA_DIR=fs.mkdtempSync(path.resolve(__dirname,'../work/browser-'));process.env.HALL_UPLOAD_DIR=path.join(process.env.HALL_DATA_DIR,'uploads');process.env.HALL_DEMO='1';
const app=require('../server'),db=require('../server/db');const bcrypt=require('bcryptjs');const password='Browser-Test-2026!';db.prepare('UPDATE users SET password=?,must_change=0').run(bcrypt.hashSync(password,4));
(async()=>{const server=app.listen(0,'127.0.0.1');await new Promise(r=>server.once('listening',r));const base='http://127.0.0.1:'+server.address().port;let browser;const result={errors:[],routes:[],unity:{}};
try{
  browser=await chromium.launch({channel:'msedge',headless:true,args:['--enable-webgl','--use-angle=swiftshader','--enable-unsafe-swiftshader','--disable-background-timer-throttling','--disable-renderer-backgrounding']});
  for(const role of ['teacher','student']){
    const login=await fetch(base+'/api/auth/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({username:role,password})});const token=(await login.json()).token;
    const context=await browser.newContext({viewport:{width:1440,height:1000}});await context.addInitScript(t=>localStorage.setItem('hall_token',t),token);
    const page=await context.newPage();page.on('pageerror',e=>result.errors.push(e.message));
    const routes=role==='teacher'?['/teacher','/teacher/resources','/teacher/students','/teacher/pushes','/teacher/review','/teacher/rubrics','/teacher/grades','/teacher/appeals']:['/','/resources/cases','/resources/ideology','/resources/projects','/resources/quiz','/workshop/cleaning','/workshop/analysis','/workshop/uncertainty','/peer/works','/peer/appeals','/grades'];
    for(const route of routes){await page.goto(base+route);await page.waitForTimeout(350);result.routes.push({role,route,text:(await page.locator('body').innerText()).slice(0,120),overflow:await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth)});}
    if(role==='teacher'){
      await page.goto(base+'/teacher/resources');await page.getByRole('button',{name:'课题包',exact:true}).click();await page.getByRole('button',{name:'新建',exact:true}).click();const modal=page.locator('.modal');await modal.locator('input').first().fill('浏览器验收课题');await modal.locator('textarea').nth(0).fill('实验指南');await modal.locator('textarea').nth(1).fill('记录完整四方向数据');await modal.getByRole('button',{name:'保存',exact:true}).click();await modal.waitFor({state:'hidden'});result.projectSave=true;
    }else{
      await page.goto(base+'/workshop/uncertainty');await page.locator('input[type=number]').nth(0).fill('1');await page.locator('input[type=number]').nth(1).fill('2');result.uncertaintyText=(await page.locator('body').innerText()).includes('1.5');
      await page.setViewportSize({width:390,height:844});await page.waitForTimeout(700);await page.screenshot({path:path.resolve(__dirname,'../work/mobile.png'),fullPage:true,animations:'disabled'});result.mobileOverflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth);await page.setViewportSize({width:1440,height:1000});
      await page.goto(base+'/sim/lab');await page.waitForFunction(()=>document.querySelector('iframe')?.contentWindow?.HallHost?.ready,null,{timeout:60000});await page.waitForTimeout(1200);
      const frame=page.frames().find(f=>f.url().endsWith('/sim/index.html'));
      result.unity.events=await frame.evaluate(()=>window.HallHost.events.map(e=>({type:e.type,code:e.code,detail:e.detail,sessionId:e.sessionId})).slice(-10));
      await frame.evaluate(()=>{window.latest=null;window.addEventListener('hall-event',e=>window.latest=e.detail);for(let i=0;i<6;i++)window.HallHost.send('ConnectTerminals',{a:i,b:i+6});window.HallHost.send('SetParameter',{name:'power',value:1});window.HallHost.send('SetParameter',{name:'IS_mA',value:3});window.HallHost.send('SetParameter',{name:'IM_A',value:.4});});
      await page.waitForTimeout(5000);await frame.evaluate(()=>window.HallHost.send('RequestSnapshot'));await page.waitForTimeout(200);result.unity.snapshot=await frame.evaluate(()=>window.latest);await page.screenshot({path:path.resolve(__dirname,'../work/unity-platform.png')});
      const send=(method,payload)=>frame.evaluate(([m,p])=>window.HallHost.send(m,p),[method,payload]);
      await frame.evaluate(()=>window.addEventListener('hall-event',e=>{if(e.detail.type==='OnSnapshot')window.testSnapshot=e.detail;}));
      for(const [is,im] of [[1,1],[1,-1],[-1,-1],[-1,1]]){
        await send('SetParameter',{name:'power',value:0});
        await send('SetParameter',{name:'isSwitch',value:is});await send('SetParameter',{name:'imSwitch',value:im});
        await send('SetParameter',{name:'power',value:1});await send('SetParameter',{name:'IS_mA',value:3});await send('SetParameter',{name:'IM_A',value:.4});
        let stable=false;
        for(let attempt=0;attempt<180;attempt++){
          await send('RequestSnapshot');await page.waitForTimeout(250);
          stable=await frame.evaluate(()=>window.testSnapshot?.state?.stable);
          if(stable)break;
        }
        assert(stable,'Software-rendered readings must stabilize');await send('RecordMeasurement');await page.waitForTimeout(300);
      }
      await page.getByRole('button',{name:'结束实验',exact:true}).click();
      await frame.waitForFunction(()=>window.HallHost.events.some(e=>e.type==='OnExperimentComplete'));
      await page.waitForFunction(()=>document.querySelector('iframe')&&[...document.querySelectorAll('button')].some(b=>b.textContent.includes('结束实验')&&b.disabled));
      await page.waitForFunction(()=>document.querySelector('[role=status]')?.textContent.trim()==='已保存',null,{timeout:20000});
      const sessions=await fetch(base+'/api/sim/sessions',{headers:{Authorization:'Bearer '+token}}).then(r=>r.json());
      const saved=await fetch(base+'/api/sim/sessions/'+sessions[0].id,{headers:{Authorization:'Bearer '+token}}).then(r=>r.json());
      assert.equal(saved.finished,1);assert.equal(saved.measurements.length,4);assert.equal(new Set(saved.measurements.map(m=>m.slot)).size,4);
      const complete=saved.measurements.find(m=>m.group_complete);assert(complete,'Database must contain complete group');
      const raw=new Map(saved.measurements.map(m=>[m.slot,m.raw_mV]));assert(Math.abs(complete.VH_mV-(raw.get(1)-raw.get(2)+raw.get(3)-raw.get(4))/4)<1e-8);
      result.unity.persisted={sessionId:saved.id,finished:saved.finished,measurements:saved.measurements.length,VH_mV:complete.VH_mV};
      result.unity.recordEvents=await frame.evaluate(()=>window.HallHost.events.slice(-5));
      result.unity.platformText=(await page.locator('body').innerText()).slice(-1000);
      assert(result.unity.platformText.includes('完整组 1'),'Duplicate bridge delivery must not duplicate groups');
      assert(!result.unity.platformText.includes('保存失败'),'Final events must be acknowledged');
    }
    await context.close();
  }
  assert.equal(result.errors.length,0);assert(result.routes.every(r=>!r.overflow));assert.equal(result.mobileOverflow,false);
}catch(e){result.failure=e.message;process.exitCode=1;}finally{if(browser)await browser.close();await new Promise(r=>server.close(r));db.close();fs.writeFileSync(path.resolve(__dirname,'../work/browser-results.json'),JSON.stringify(result,null,2));console.log(JSON.stringify({...result,unity:{...result.unity,snapshot:result.unity.snapshot?{type:result.unity.snapshot.type,state:result.unity.snapshot.state}:undefined}},null,2));}
})();
