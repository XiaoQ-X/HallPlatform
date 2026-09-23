const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const dependency=name=>process.env.HALL_NODE_MODULES?require(path.join(process.env.HALL_NODE_MODULES,name)):require(name);
const {chromium}=dependency('playwright');
const sharp=dependency('sharp');
const software=process.env.HALL_SOFTWARE_RENDERER==='1';
const root=path.resolve(software?'Logs/WebBrowserChecks-software':'Logs/WebBrowserChecks');fs.mkdirSync(root,{recursive:true});
(async()=>{
 const chrome='C:/Program Files/Google/Chrome/Application/chrome.exe';
 const browser=await chromium.launch({executablePath:process.env.HALL_BROWSER||(fs.existsSync(chrome)?chrome:undefined),headless:true,args:['--enable-webgl','--ignore-gpu-blocklist',...(software?['--use-angle=swiftshader','--enable-unsafe-swiftshader']:[])]});
 const page=await browser.newPage({viewport:{width:1600,height:1000}});const errors=[];
 page.on('pageerror',e=>errors.push(String(e)));
 page.on('console',m=>{if(m.type()==='error')errors.push(m.text());});
 try {
 await page.goto('http://127.0.0.1:8090/handoff.html');
 const frame=page.frames().find(f=>f.url().endsWith('/index.html'));
 assert(frame,'iframe');
 await frame.waitForFunction(()=>window.HallHost?.ready,{},{timeout:240000});
 await page.waitForTimeout(2500);
 await frame.evaluate(()=>{window.testEvents=[];window.addEventListener('hall-event',e=>window.testEvents.push(e.detail));});
 const send=async(method,payload)=>frame.evaluate(([m,p])=>window.HallHost.send(m,p),[method,payload]);
 const settle=()=>page.waitForTimeout(150);
 const last=()=>frame.evaluate(()=>window.testEvents.at(-1));
 const waitStable=async()=>{
   for(let i=0;i<200;i++){await send('RequestSnapshot');await settle();if((await last()).state.stable)return;}
   throw Error('Readings did not stabilize');
 };
 let abnormalCursor=0;
 const abnormal=async code=>{await settle();const result=await frame.evaluate(([c,start])=>({found:window.testEvents.slice(start).some(e=>e.type==='OnAbnormalEvent'&&e.code===c),count:window.testEvents.length}),[code,abnormalCursor]);assert(result.found,code);abnormalCursor=result.count;};
 await page.screenshot({path:path.join(root,'01-desktop.png')});
 const canvasImage=await frame.locator('canvas').screenshot();
 const {data,info}=await sharp(canvasImage).resize(160,100).raw().toBuffer({resolveWithObject:true});
 const colors=new Set();for(let i=0;i<data.length;i+=info.channels)colors.add(data[i]+','+data[i+1]+','+data[i+2]);
 assert(colors.size>100,'Canvas is blank');
 await send('SetParameter',{name:'power',value:1});await abnormal('CIRCUIT_INCOMPLETE');
 await send('SetParameter',{name:'IS_mA',value:100});await abnormal('INVALID_PARAMETER');
 await send('SetParameter',{name:'IS_mA'});await abnormal('INVALID_PARAMETER');
 await send('InitExperiment',{schemaVersion:1,caseId:'bad',material:'other',thickness_mm:.5,maxIs_mA:10,maxIm_A:1});await abnormal('INVALID_INIT');
 await send('GotoStep',{step:99});await abnormal('INVALID_STEP');
 await send('FinishFromWeb');await abnormal('EXPERIMENT_INCOMPLETE');
 await send('ResetExperiment');await settle();
 await send('InitExperiment',{schemaVersion:1,caseId:'browser-check',material:'n-silicon',thickness_mm:.5,maxIs_mA:5,maxIm_A:.6});
 for(let i=0;i<6;i++)await send('ConnectTerminals',{a:i,b:i+6});
 await send('SetParameter',{name:'power',value:1});
 await send('ConnectTerminals',{a:0,b:7});await abnormal('LIVE_WIRING_CHANGE');
 await send('SetParameter',{name:'isSwitch',value:-1});await abnormal('LIVE_SWITCH_CHANGE');
 await send('SetParameter',{name:'IS_mA',value:3});await send('SetParameter',{name:'IM_A',value:.4});
 await settle();
 await send('RecordMeasurement');await abnormal('READING_UNSTABLE');
 await send('GotoStep',{step:5});
 await frame.evaluate(()=>{
   const stream=document.querySelector('canvas').captureStream(15);window.recordChunks=[];
   window.recorder=new MediaRecorder(stream,{mimeType:'video/webm;codecs=vp8',videoBitsPerSecond:2200000});
   window.recorder.ondataavailable=e=>{if(e.data.size)window.recordChunks.push(e.data);};window.recorder.start(1000);
 });
 for(const [is,im] of [[1,1],[1,-1],[-1,-1],[-1,1]]){
   await send('SetParameter',{name:'power',value:0});await send('SetParameter',{name:'isSwitch',value:is});await send('SetParameter',{name:'imSwitch',value:im});
   await send('SetParameter',{name:'power',value:1});await send('SetParameter',{name:'IS_mA',value:3});await send('SetParameter',{name:'IM_A',value:.4});
   await waitStable();await send('RecordMeasurement');await settle();
 }
 await send('FinishFromWeb');await settle();
 const completion=await frame.evaluate(()=>window.testEvents.findLast(e=>e.type==='OnExperimentComplete'));
 assert(completion&&completion.rows.length===1,'Complete result missing');
 const row=completion.rows[0];assert(Math.abs(row.vh-(row.v1-row.v2+row.v3-row.v4)/4)<.00001,'Symmetric formula');
 assert(row.b1>0&&row.b2<0&&row.b3<0&&row.b4>0,'Signed B');
 const measurements=completion.history.map(e=>JSON.parse(e)).filter(e=>e.type==='OnMeasureData');
 assert.equal(measurements.length,4);assert.equal(new Set(measurements.map(e=>e.measurement.slot)).size,4);
 assert(measurements[3].measurement.groupComplete);assert.equal(completion.state.pendingDirections,0);
 await send('RecordMeasurement');await abnormal('SESSION_FINISHED');
 const video=await frame.evaluate(()=>new Promise(resolve=>{
   window.recorder.onstop=async()=>{const blob=new Blob(window.recordChunks,{type:'video/webm'}),reader=new FileReader();reader.onload=()=>resolve(reader.result.split(',')[1]);reader.readAsDataURL(blob);};window.recorder.stop();
 }));fs.writeFileSync(path.join(root,'four-direction-operation.webm'),Buffer.from(video,'base64'));
 await page.screenshot({path:path.join(root,'02-complete.png')});
 const downloadPromise=page.waitForEvent('download');await page.getByRole('button',{name:'下载 JSON',exact:true}).click();
 const download=await downloadPromise;await download.saveAs(path.join(root,'session-export.json'));
 assert(JSON.parse(fs.readFileSync(path.join(root,'session-export.json'),'utf8')).events.some(e=>e.type==='OnExperimentComplete'));
 await send('ResetExperiment');await settle();await send('RequestSnapshot');await settle();assert.equal((await last()).rows.length,0);
 await page.setViewportSize({width:390,height:844});await page.waitForTimeout(1200);
 await page.screenshot({path:path.join(root,'03-mobile.png'),fullPage:true});
 assert(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth),'Host horizontal overflow');
 const mobileImage=await frame.locator('canvas').screenshot();const stats=await sharp(mobileImage).stats();assert(stats.channels.some(c=>c.stdev>15),'Mobile canvas blank');
 await page.setViewportSize({width:1600,height:1000});await settle();
 // Verify host callbacks and same-origin iframe command routing.
 await frame.evaluate(()=>{window.OnSnapshot=e=>window.callbackSnapshot=e;});
 await page.evaluate(()=>document.querySelector('iframe').contentWindow.postMessage({source:'hall-platform',method:'RequestSnapshot',requestId:'probe'},location.origin));
 await settle();assert(await frame.evaluate(()=>window.callbackSnapshot?.type==='OnSnapshot'));
 const result={passed:errors.length===0,canvasColors:colors.size,groups:completion.rows.length,measurements:measurements.length,errors,completion};
 fs.writeFileSync(path.join(root,'result.json'),JSON.stringify(result,null,2));
 assert.equal(errors.length,0,'Browser errors: '+errors.join('\n'));
 console.log('PASS: WebGL rendering, four directions, events, abnormal cases, reset, downloads, mobile framing, callback and iframe routing');
 }catch(error){
   fs.writeFileSync(path.join(root,'failure.json'),JSON.stringify({error:String(error),errors,events:await page.frames().find(f=>f.url().endsWith('/index.html'))?.evaluate(()=>window.testEvents).catch(()=>[])},null,2));
   throw error;
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
