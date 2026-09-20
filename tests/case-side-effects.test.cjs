const test=require('node:test'),assert=require('node:assert/strict'),fs=require('fs'),path=require('path'),bcrypt=require('bcryptjs');
const work=path.resolve(__dirname,'../work');fs.mkdirSync(work,{recursive:true});process.env.HALL_DATA_DIR=fs.mkdtempSync(path.join(work,'case-se-'));process.env.HALL_UPLOAD_DIR=path.join(process.env.HALL_DATA_DIR,'uploads');process.env.HALL_DEMO='1';
const app=require('../server'),db=require('../server/db');
const password='CaseSE-Test-2026!';db.prepare('UPDATE users SET password=?,must_change=0').run(bcrypt.hashSync(password,4));

// 与占位案例一致的物理模型（用于生成自洽读数）
const RH=-2.7e-4,dm=0.0005,r0=0.05,kE=0.005,IS=5,IM=0.8,B=0.4;
function readingAt(si,sb){
  const sIS=IS*si,sB=B*sb;
  const VH=RH*sIS*sB/dm,V0=r0*sIS,VE=kE*sIS*sB;
  return {VH,V0,VE,total:VH+V0+VE};
}

test('Side-effects Unity case integration',async()=>{
 const server=app.listen(0,'127.0.0.1');await new Promise(r=>server.once('listening',r));const base='http://127.0.0.1:'+server.address().port;
 const call=async(url,token,body,method)=>{const response=await fetch(base+'/api'+url,{method:method||(body?'POST':'GET'),headers:{...(token?{Authorization:'Bearer '+token}:{}),...(body?{'Content-Type':'application/json'}:{})},body:body?JSON.stringify(body):undefined});return{status:response.status,data:await response.json().catch(()=>null)};};
 try{
  const student=(await call('/auth/login',null,{username:'student',password})).data.token;
  let seq=0;
  const unitySid='unity-se-'+Date.now();
  const mkEvent=(type,extra)=>({schemaVersion:1,step:1,type,eventId:unitySid+':'+(++seq),sessionId:unitySid,caseId:'case-side-effects',timestamp:new Date().toISOString(),success:true,isDemo:false,maxIs_mA:10,maxIm_A:1,...extra});

  await test('built-in case starts without case library',async()=>{
    const r=await call('/sim/start',student,{case_id:'case-side-effects'});
    assert.equal(r.status,200);assert.equal(r.data.config.module,'side-effects');
    assert.ok(r.data.platformSessionId);
  });
  const psid=(await call('/sim/start',student,{case_id:'case-side-effects'})).data.platformSessionId;

  await test('four-direction measurements persist with V0/VE breakdown',async()=>{
    const quad=[[1,1],[1,-1],[-1,-1],[-1,1]];
    const events=quad.map((q,i)=>{
      const slot=i+1,r=readingAt(q[0],q[1]);
      return mkEvent('OnMeasureData',{measurement:{groupId:'G1',timestamp:new Date().toISOString(),slot,
        isDirection:q[0],imDirection:q[1],voltageDirection:1,
        IS_mA:IS*q[0],IM_A:IM*q[1],isSetpoint_mA:IS,imSetpoint_A:IM,B_T:B*q[1],
        rawVoltage_mV:r.total,VH_mV:r.VH,normalizedVH_mV:r.VH,V0_mV:r.V0,VE_mV:r.VE,
        groupComplete:slot===4,row:null}});
    });
    const r=await call('/sim/events',student,{platformSessionId:psid,events});
    assert.equal(r.status,200);assert.equal(r.data.saved,4);
    const rows=db.prepare('SELECT * FROM sim_measurements WHERE session_id=? ORDER BY slot').all(psid);
    assert.equal(rows.length,4);
    // 第4点合成 VH = VH+VE（VE 残留），约 -1.07 mV
    const complete=rows.find(x=>x.group_complete===1);
    assert.ok(complete);assert.ok(Math.abs(complete.VH_mV-(-1.07))<1e-9);
    // 分项随 payload 保存（无表迁移）
    const payload=JSON.parse(rows[0].payload);
    assert.equal(payload.V0_mV,0.25);assert.ok(Math.abs(payload.VE_mV-0.01)<1e-9);
  });

  await test('completion saves full report in session state',async()=>{
    const V={};[1,2,3,4].forEach((slot,i)=>{const q=[[1,1],[1,-1],[-1,-1],[-1,1]][i];V['V'+slot]=readingAt(q[0],q[1]).total;});
    const corrected=(V.V1-V.V2+V.V3-V.V4)/4,VEref=readingAt(1,1).VE,ideal=corrected-VEref;
    const report={module:'side-effects',completedAt:new Date().toISOString(),
      params:{IS_mA:IS,IM_A:IM,B_T:B,RH,d_mm:0.5,r0,kE},
      forwardReadings:{V1:V.V1,V3:V.V3},reverseReadings:{V2:V.V2,V4:V.V4},
      readings:V,beforeCorrection_mV:V.V1,correctedHall_mV:corrected,residualVE_mV:VEref,
      afterCorrection_mV:ideal,operations:[{time:'x',text:'记录'}]};
    const fin=mkEvent('OnExperimentComplete',{state:{powered:false,finished:true,report}});
    const r=await call('/sim/events',student,{platformSessionId:psid,events:[fin]});
    assert.equal(r.status,200);
    const detail=(await call('/sim/sessions/'+psid,student)).data;
    assert.equal(detail.finished,1);
    assert.equal(detail.state.report.module,'side-effects');
    assert.ok(Math.abs(detail.state.report.afterCorrection_mV-(-1.08))<1e-9);
    assert.ok(detail.state.report.operations.length>=1);
    assert.ok(detail.state.report.completedAt);
  });

  await test('cannot finish without a complete four-direction group',async()=>{
    const other=(await call('/sim/start',student,{case_id:'case-side-effects'})).data.platformSessionId;
    const fin=mkEvent('OnExperimentComplete',{state:{finished:true}});
    const r=await call('/sim/events',student,{platformSessionId:other,events:[fin]});
    assert.equal(r.status,400);
  });

  await test('unknown non-builtin case is rejected',async()=>{
    const r=await call('/sim/start',student,{case_id:'case-999999'});
    assert.equal(r.status,400);
  });
 }finally{await new Promise(r=>server.close(r));db.close();}
});
