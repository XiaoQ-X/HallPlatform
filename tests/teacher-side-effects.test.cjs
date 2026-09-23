const test=require('node:test'),assert=require('node:assert/strict'),fs=require('fs'),path=require('path'),bcrypt=require('bcryptjs');
const work=path.resolve(__dirname,'../work');fs.mkdirSync(work,{recursive:true});process.env.HALL_DATA_DIR=fs.mkdtempSync(path.join(work,'teacher-se-'));process.env.HALL_UPLOAD_DIR=path.join(process.env.HALL_DATA_DIR,'uploads');process.env.HALL_DEMO='1';
const app=require('../server'),db=require('../server/db');
const password='TeacherSE-Test-2026!';db.prepare('UPDATE users SET password=?,must_change=0').run(bcrypt.hashSync(password,4));

const RH=-2.7e-4,dm=0.0005,r0=0.05,kE=0.005,IS=5,IM=0.8,B=0.4;

test('Teacher side-effects management, permissions and audit',async()=>{
 const server=app.listen(0,'127.0.0.1');await new Promise(r=>server.once('listening',r));const base='http://127.0.0.1:'+server.address().port;
 const call=async(url,token,body,method)=>{const response=await fetch(base+'/api'+url,{method:method||(body?'POST':'GET'),headers:{...(token?{Authorization:'Bearer '+token}:{}),...(body?{'Content-Type':'application/json'}:{})},body:body?JSON.stringify(body):undefined});return{status:response.status,data:await response.json().catch(()=>null)};};
 try{
  const teacher=(await call('/auth/login',null,{username:'teacher',password})).data.token;
  const student=(await call('/auth/login',null,{username:'student',password})).data.token;
  const li=(await call('/auth/login',null,{username:'li',password})).data.token;
  const studentId=db.prepare("SELECT id FROM users WHERE username='student'").get().id;
  const liId=db.prepare("SELECT id FROM users WHERE username='li'").get().id;

  // 完整 Unity 流程
  async function completeUnity(token){
    const psid=(await call('/sim/start',token,{case_id:'case-side-effects'})).data.platformSessionId;
    const unity='unity-'+Date.now();let n=0;
    const quad=[[1,1],[1,-1],[-1,-1],[-1,1]];
    const events=quad.map((q,i)=>{
      const slot=i+1,sIS=IS*q[0],sB=B*q[1];
      const vh=RH*sIS*sB/dm,v0=r0*sIS,ve=kE*sIS*sB,total=vh+v0+ve;
      return {schemaVersion:1,step:1,type:'OnMeasureData',eventId:unity+':'+(++n),sessionId:unity,caseId:'case-side-effects',timestamp:new Date().toISOString(),success:true,isDemo:false,maxIs_mA:10,maxIm_A:1,
        measurement:{groupId:'G1',timestamp:new Date().toISOString(),slot,isDirection:q[0],imDirection:q[1],voltageDirection:1,IS_mA:sIS,IM_A:IM*q[1],isSetpoint_mA:IS,imSetpoint_A:IM,B_T:sB,rawVoltage_mV:total,VH_mV:vh,normalizedVH_mV:vh,V0_mV:v0,VE_mV:ve,groupComplete:slot===4,row:null}};
    });
    await call('/sim/events',token,{platformSessionId:psid,events});
    const v1=events[0].measurement.rawVoltage_mV;
    const fin={schemaVersion:1,step:1,type:'OnExperimentComplete',eventId:unity+':'+(++n),sessionId:unity,caseId:'case-side-effects',timestamp:new Date().toISOString(),success:true,isDemo:false,
      state:{finished:true,report:{completedAt:new Date().toISOString(),params:{IS_mA:IS,IM_A:IM,B_T:B,r0,kE},readings:{},beforeCorrection_mV:v1,correctedHall_mV:-1.07,residualVE_mV:0.01,afterCorrection_mV:-1.08,operations:[]}}};
    await call('/sim/events',token,{platformSessionId:psid,events:[fin]});
    return psid;
  }

  await test('teacher list starts as not_started',async()=>{
    const r=await call('/teacher/side-effects',teacher);
    assert.equal(r.status,200);assert.ok(r.data.length>=6);
    const me=r.data.find(x=>x.id===studentId);
    assert.equal(me.status,'not_started');
  });

  const sessionId=await completeUnity(student);

  await test('completed unity flow shows completed, reversal and key readings',async()=>{
    const r=await call('/teacher/side-effects',teacher);
    const me=r.data.find(x=>x.id===studentId);
    assert.equal(me.status,'completed');
    assert.equal(me.reversal,true);
    assert.ok(Math.abs(me.key.corrected_mV-(-1.07))<1e-9);
    assert.ok(me.lastTime);
  });

  await test('student submission with conclusion shows submitted',async()=>{
    const conclusion='V0 来自电极偏移，可由四方向对称法消除；VE 与 VH 同规律而残留。';
    const r=await call('/sim/side-effects',student,{
      title:'副效应记录',conclusion,
      params:{I_mA:IS,B_T:B,RH,d_mm:0.5,r0,kE},
      readings:{V1:-0.82,V2:1.32,V3:-1.32,V4:0.82},
      result:{idealVH_mV:-1.08,residualVE_mV:0.01,correctedHall_mV:-1.07}});
    assert.equal(r.status,200);
    const list=(await call('/teacher/side-effects',teacher)).data.find(x=>x.id===studentId);
    assert.equal(list.status,'submitted');
    const detail=(await call('/teacher/side-effects/student/'+studentId,teacher)).data;
    assert.equal(detail.records[0].conclusion,conclusion);
  });

  await test('server recomputes side-effects formula and rejects forged readings',async()=>{
    const r=await call('/sim/side-effects',student,{
      title:'篡改读数',conclusion:'这是用于验证服务端复算的无效记录。',
      params:{I_mA:IS,B_T:B,RH,d_mm:0.5,r0,kE},
      readings:{V1:0,V2:0,V3:0,V4:0},
      result:{idealVH_mV:0,residualVE_mV:0,correctedHall_mV:0}});
    assert.equal(r.status,400);
  });

  await test('server verifies four transverse-effect components and keeps VN/VRL out of Vcorr',async()=>{
    const r=await call('/sim/side-effects',student,{
      title:'四种横向效应复核',conclusion:'霍尔、厄廷豪森、能斯特和里纪勒杜克是四种横向效应；四方向修正消除 V0、VN、VRL。',
      params:{I_mA:IS,B_T:B,RH,d_mm:0.5,r0,kE,kN_mV_T:0.021,kRL_mV_T:0.008},
      readings:{V1:-0.8084,V2:1.3084,V3:-1.3316,V4:0.8316},
      result:{idealVH_mV:-1.08,residualVE_mV:0.01,correctedHall_mV:-1.07}
    });
    assert.equal(r.status,200);
    const saved=db.prepare('SELECT params_json,result_json FROM side_effect_records WHERE id=?').get(r.data.id);
    assert.equal(JSON.parse(saved.params_json).kN_mV_T,0.021);
    assert.equal(JSON.parse(saved.params_json).kRL_mV_T,0.008);
    assert.equal(JSON.parse(saved.result_json).correctedHall_mV,-1.07);
  });

  await test('server rejects incomplete side-effects submission',async()=>{
    const r=await call('/sim/side-effects',student,{title:'不完整记录',params:{I_mA:IS,B_T:B},readings:{V1:1},result:{}});
    assert.equal(r.status,400);
  });

  await test('grading persists, sets graded and writes audit',async()=>{
    const scores={dim_params:8,dim_steps:7,dim_v0:9,dim_ve:6,dim_reversal:8,dim_explain:7};
    const r=await call('/teacher/side-effects/grade',teacher,{student_id:studentId,scores,comment:'过程完整'});
    assert.equal(r.status,200);
    const list=(await call('/teacher/side-effects',teacher)).data.find(x=>x.id===studentId);
    assert.equal(list.status,'graded');
    assert.ok(Math.abs(list.grade.total-(Object.values(scores).reduce((a,b)=>a+b,0)/60*100))<1e-9);
    const audits=db.prepare("SELECT * FROM audit_log WHERE action='side-effect-grade'").all();
    assert.equal(audits.length,1);
  });

  await test('re-grading appends history and writes update audit',async()=>{
    const scores={dim_params:9,dim_steps:8,dim_v0:9,dim_ve:7,dim_reversal:9,dim_explain:8};
    const r=await call('/teacher/side-effects/grade',teacher,{student_id:studentId,scores,comment:'复核后',total:90});
    assert.equal(r.status,200);
    assert.equal(db.prepare("SELECT COUNT(*) c FROM audit_log WHERE action='side-effect-grade-update'").get().c,1);
    const detail=(await call('/teacher/side-effects/student/'+studentId,teacher)).data;
    assert.equal(detail.grades.length,2);
    assert.equal(Math.round(detail.grades[0].total),90);
  });

  await test('partial dimensions without total rejected',async()=>{
    const r=await call('/teacher/side-effects/grade',teacher,{student_id:studentId,scores:{dim_params:8}});
    assert.equal(r.status,400);
  });

  await test('permission: students cannot reach teacher endpoints',async()=>{
    assert.equal((await call('/teacher/side-effects',student)).status,403);
    assert.equal((await call('/teacher/side-effects/student/'+studentId,student)).status,403);
  });

  await test('permission: student cannot read another student session',async()=>{
    const r=await call('/sim/sessions/'+sessionId,li);
    assert.equal(r.status,403);
  });

  await test('permission: teacher cannot grade a student outside their classes',async()=>{
    const hash=bcrypt.hashSync('x',4);
    db.prepare("INSERT INTO users(username,password,name,role) VALUES('otherteacher',?,'外校教师','teacher')").run(hash);
    const otherTeacher=db.prepare("SELECT id FROM users WHERE username='otherteacher'").get().id;
    db.prepare("INSERT INTO classes(name,code,teacher_id) VALUES('外班','OUT',?)").run(otherTeacher);
    const outCls=db.prepare("SELECT id FROM classes WHERE code='OUT'").get().id;
    db.prepare("INSERT INTO users(username,password,name,role,class_id) VALUES('outstu',?,'外班生','student',?)").run(hash,outCls);
    const outStu=db.prepare("SELECT id FROM users WHERE username='outstu'").get().id;
    const r=await call('/teacher/side-effects/grade',teacher,{student_id:outStu,total:80});
    assert.equal(r.status,403);
  });
 }finally{await new Promise(r=>server.close(r));db.close();}
});
