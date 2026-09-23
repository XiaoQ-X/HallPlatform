const r=require('express').Router(),bcrypt=require('bcryptjs');const {db,fail,parse,text,number,id,transaction,audit,studentAccess,classAccess}=require('../common');const {forStudent}=require('../grades');const presentation=require('../presentation-identity');
function students(req){return db.prepare("SELECT u.id,u.username,u.name,u.avatar,u.student_no,u.class_id,u.active,c.name class_name FROM users u JOIN classes c ON c.id=u.class_id WHERE u.role='student' AND c.teacher_id=? ORDER BY u.id").all(req.user.id).filter(s=>!req.query.class_id||s.class_id===Number(req.query.class_id)).map(s=>presentation.enabled(db)?(()=>{const a=presentation.alias(db,s.id);const label=a?'学生账号'+a.ordinal:'学生账号待分配';const out={...s,name:a?'同学'+a.ordinal:'同学待分配',student_no:a?String(a.ordinal):null,account_label:label};delete out.username;return out;})():s);}
r.get('/students',(req,res)=>res.json(students(req).map(s=>{
  const sessions=db.prepare('SELECT COUNT(*) sessions,COALESCE(SUM(finished),0) finished FROM sim_sessions WHERE student_id=? AND demo=0').get(s.id);
  const errors=db.prepare('SELECT COUNT(*) n FROM sim_abnormals a JOIN sim_sessions s ON s.id=a.session_id WHERE s.student_id=? AND s.demo=0').get(s.id).n;
  const sub=db.prepare('SELECT COUNT(*) subs,AVG(grade) projectAvg FROM project_submissions WHERE student_id=?').get(s.id);return {...s,...sessions,...sub,errors,quizBest:forStudent(s).comp.quiz.score};
})));
r.get('/grades',(req,res)=>res.json(students(req).map(forStudent)));
r.get('/dashboard',(req,res)=>{
  const ss=students(req),ids=new Set(ss.map(s=>s.id));const sessions=db.prepare('SELECT * FROM sim_sessions WHERE demo=0').all().filter(s=>ids.has(s.student_id));
  const sid=new Set(sessions.map(s=>s.id));const subs=db.prepare('SELECT * FROM project_submissions').all().filter(s=>ids.has(s.student_id));const abnormals=db.prepare('SELECT * FROM sim_abnormals').all().filter(a=>sid.has(a.session_id));const dist={};for(const a of abnormals)dist[a.code]=(dist[a.code]||0)+1;
  const dailySessions=[];for(let offset=6;offset>=0;offset--){const d=new Date();d.setDate(d.getDate()-offset);const day=[d.getFullYear(),String(d.getMonth()+1).padStart(2,'0'),String(d.getDate()).padStart(2,'0')].join('-');dailySessions.push({d:day,c:sessions.filter(s=>s.started_at.startsWith(day)).length});}
  const grades=ss.map(forStudent),avg=k=>grades.length?Math.round(grades.reduce((a,g)=>a+g.comp[k].score,0)/grades.length*10)/10:0;
  const resourceCount={};for(const t of ['cases','ideology','questions','projects'])resourceCount[t]=db.prepare(`SELECT COUNT(*) n FROM ${t} WHERE published=1 AND archived=0 AND owner_id=?`).get(req.user.id).n;
  res.json({studentCount:ss.length,classCount:db.prepare('SELECT COUNT(*) n FROM classes WHERE teacher_id=?').get(req.user.id).n,sessionCount:sessions.length,finishedCount:sessions.filter(s=>s.finished).length,completionRate:ss.length?Math.round(new Set(sessions.filter(s=>s.finished).map(s=>s.student_id)).size/ss.length*100):0,subCount:subs.length,gradedCount:subs.filter(s=>s.grade!=null).length,quizAvg:avg('quiz'),projectAvg:avg('project'),abnormalDist:Object.entries(dist).map(([code,count])=>({code,count})),dailySessions,resourceCount,unresolved:db.prepare('SELECT c.student_id FROM cleaning_records c WHERE resolved=0').all().filter(c=>ids.has(c.student_id)).length,unverified:sessions.filter(s=>s.finished&&!s.verified).length});
});
r.get('/sessions',(req,res)=>{const ids=new Set(students(req).map(s=>s.id));res.json(db.prepare('SELECT s.*,u.name student_name FROM sim_sessions s JOIN users u ON u.id=s.student_id WHERE s.demo=0 ORDER BY s.id DESC').all().filter(s=>ids.has(s.student_id)));});
r.post('/sessions/:id/verify',(req,res)=>{const s=db.prepare('SELECT * FROM sim_sessions WHERE id=? AND demo=0 AND finished=1').get(id(req.params.id));if(!s)fail('会话不可核验');studentAccess(req.user,s.student_id);const score=number(req.body.score,'实验分',0,100);const reason=text(req.body.reason,'核验依据');transaction(()=>{db.prepare('UPDATE sim_sessions SET verified=1,quality_score=? WHERE id=?').run(score,s.id);audit(req.user.id,'sim-verify',s.id,s,{score,reason});});res.json({ok:1});});
 r.post('/classes',(req,res)=>{let name=text(req.body.name,'班级名称',100),code=String(req.body.code||'').slice(0,50);if(presentation.enabled(db)){const n=presentation.nextClassOrdinal(db);name='班级'+n;code='CLASS'+n;}if(db.prepare('SELECT id FROM classes WHERE name=?').get(name))fail('班级已存在');const info=transaction(()=>{const row=db.prepare('INSERT INTO classes(name,code,teacher_id) VALUES(?,?,?)').run(name,code,req.user.id);if(presentation.enabled(db))db.prepare('INSERT INTO presentation_class_aliases(class_id,ordinal) VALUES(?,?)').run(row.lastInsertRowid,Number(name.slice(2)));return row;});res.json({id:info.lastInsertRowid});});
function studentInput(req,key){const b=req.body;classAccess(req.user,b.class_id);const anonymous=presentation.enabled(db);let name=anonymous?'匿名':text(b.name,'姓名',80),no=anonymous?'0':text(b.student_no,'学号',80);if(anonymous){const existing=key&&db.prepare('SELECT student_no FROM users WHERE id=?').get(key);const a=key?(presentation.alias(db,key)||(/^[1-9]\d*$/.test(existing?.student_no||'')?{ordinal:Number(existing.student_no)}:null)):{ordinal:presentation.nextOrdinal(db,'student')};const ordinal=a?.ordinal||presentation.nextOrdinal(db,'student');name='同学'+ordinal;no=String(ordinal);}if(db.prepare('SELECT id FROM users WHERE class_id=? AND student_no=? AND id<>?').get(b.class_id,no,key||0))fail('班级内学号重复');return{name,no,classId:id(b.class_id),avatar:String(b.avatar||'学生').slice(0,30)};}
function pass(value){text(value,'密码',72);if(value.length<6)fail('密码至少6位');return bcrypt.hashSync(value,12);}
r.post('/students',(req,res)=>{const b=req.body,v=studentInput(req),username=text(b.username,'账号',80),hash=pass(b.password);if(db.prepare('SELECT id FROM users WHERE username=?').get(username))fail('账号已存在');const info=transaction(()=>{const row=db.prepare("INSERT INTO users(username,password,name,role,class_id,student_no,avatar) VALUES(?,?,?,'student',?,?,?)").run(username,hash,v.name,v.classId,v.no,v.avatar);if(presentation.enabled(db))db.prepare("INSERT INTO presentation_user_aliases(user_id,role,ordinal) VALUES(?,'student',?)").run(row.lastInsertRowid,Number(v.no));return row;});res.json({id:info.lastInsertRowid});});
r.put('/students/:id',(req,res)=>{const old=studentAccess(req.user,req.params.id),v=studentInput(req,old.id);transaction(()=>{db.prepare("UPDATE users SET name=?,class_id=?,student_no=?,avatar=?,token_version=token_version+1 WHERE id=? AND role='student'").run(v.name,v.classId,v.no,v.avatar,old.id);if(presentation.enabled(db)&&!presentation.alias(db,old.id))db.prepare("INSERT INTO presentation_user_aliases(user_id,role,ordinal) VALUES(?,'student',?)").run(old.id,Number(v.no));if(old.class_id!==v.classId)db.prepare('DELETE FROM review_assignments WHERE (reviewer_id=? OR item_id IN (SELECT id FROM review_items WHERE student_id=?)) AND NOT EXISTS(SELECT 1 FROM review_items i JOIN users owner ON owner.id=i.student_id JOIN users reviewer ON reviewer.id=review_assignments.reviewer_id WHERE i.id=review_assignments.item_id AND owner.class_id=reviewer.class_id)').run(old.id,old.id);audit(req.user.id,'student-update',old.id,old,v);});res.json({ok:1});});
r.post('/students/:id/reset-password',(req,res)=>{const s=studentAccess(req.user,req.params.id),hash=pass(req.body.password);db.prepare('UPDATE users SET password=?,must_change=0,token_version=token_version+1 WHERE id=?').run(hash,s.id);audit(req.user.id,'password-reset',s.id,null,null);res.json({ok:1});});
r.post('/grades/override',(req,res)=>{const b=req.body;studentAccess(req.user,b.student_id);if(!['sim','quiz','project','peer'].includes(b.component))fail('成绩项无效');number(b.score,'分数',0,100);text(b.reason,'调整理由');transaction(()=>{const old=db.prepare('SELECT * FROM grade_overrides WHERE student_id=? AND component=?').get(b.student_id,b.component);if(old)db.prepare('UPDATE grade_overrides SET score=? WHERE id=?').run(b.score,old.id);else db.prepare('INSERT INTO grade_overrides(student_id,component,score,max_score) VALUES(?,?,?,100)').run(b.student_id,b.component,b.score);audit(req.user.id,'grade-override',b.student_id,old,b);});res.json({ok:1});});
r.delete('/grades/override',(req,res)=>{const b=req.body;studentAccess(req.user,b.student_id);text(b.reason,'清除理由');transaction(()=>{const old=db.prepare('SELECT * FROM grade_overrides WHERE student_id=? AND component=?').get(b.student_id,b.component);db.prepare('DELETE FROM grade_overrides WHERE student_id=? AND component=?').run(b.student_id,b.component);audit(req.user.id,'grade-clear',b.student_id,old,b);});res.json({ok:1});});
r.post('/grades/publish',(req,res)=>{const reason=text(req.body.reason,'发布说明');const rows=students(req).map(forStudent);for(const g of rows){const pendingQuiz=db.prepare("SELECT 1 FROM quiz_attempts a JOIN quizzes q ON q.id=a.quiz_id WHERE a.student_id=? AND a.status='pending' AND q.published=1 AND q.archived=0 AND q.owner_id=?").get(g.id,req.user.id);const pendingProject=db.prepare('SELECT 1 FROM project_submissions s JOIN projects p ON p.id=s.project_id WHERE s.student_id=? AND s.grade IS NULL AND p.published=1 AND p.archived=0 AND p.owner_id=?').get(g.id,req.user.id);if(pendingQuiz||pendingProject)fail(g.name+'存在待评答卷或课题，请先完成教学评阅',409);}transaction(()=>{for(const g of rows)db.prepare('INSERT INTO grade_publications(student_id,teacher_id,payload) VALUES(?,?,?)').run(g.id,req.user.id,JSON.stringify({...g,reason}));audit(req.user.id,'grade-publish','class',null,{reason,count:rows.length});});res.json({count:rows.length});});
r.get('/audit',(req,res)=>res.json(db.prepare('SELECT * FROM audit_log WHERE actor_id=? ORDER BY id DESC LIMIT 200').all(req.user.id)));
r.get('/pushes',(req,res)=>res.json(db.prepare("SELECT p.*,CASE WHEN p.target_type='class' THEN c.name ELSE u.name END target_name FROM pushes p LEFT JOIN classes c ON c.id=p.target_id LEFT JOIN users u ON u.id=p.target_id WHERE p.teacher_id=? ORDER BY p.id DESC").all(req.user.id)));
r.post('/pushes',(req,res)=>{
  const b=req.body;const title=text(b.title,'标题',200),message=text(b.message,'内容');let targets=[];
  if(b.target_type==='class'){classAccess(req.user,b.target_id);targets=db.prepare("SELECT id FROM users WHERE class_id=? AND role='student'").all(b.target_id);}else if(b.target_type==='student'){studentAccess(req.user,b.target_id);targets=[{id:id(b.target_id)}];}else fail('目标类型无效');
  const map={case:['cases','/resources/cases'],ideology:['ideology','/resources/ideology'],quiz:['quizzes','/resources/quiz'],project:['projects','/resources/projects']};let link='';if(b.resource_type){const def=map[b.resource_type];if(!def||!db.prepare(`SELECT id FROM ${def[0]} WHERE id=? AND published=1 AND archived=0 AND owner_id=?`).get(id(b.resource_id),req.user.id))fail('资源不可推送');link=def[1]+'?id='+b.resource_id;}
  transaction(()=>{db.prepare('INSERT INTO pushes(teacher_id,target_type,target_id,title,message,resource_type,resource_id) VALUES(?,?,?,?,?,?,?)').run(req.user.id,b.target_type,b.target_id,title,message,b.resource_type||null,b.resource_id||null);for(const t of targets)db.prepare('INSERT INTO notifications(student_id,title,content,link) VALUES(?,?,?,?)').run(t.id,title,message,link);});res.json({ok:1,recipients:targets.length});
});
/* ===== 副效应与误差修正 · 教师管理 ===== */
const SE_DIMS=[['dim_params','参数设置'],['dim_steps','测量步骤'],['dim_v0','识别V0'],['dim_ve','识别VE'],['dim_reversal','换向修正'],['dim_explain','结果解释']];
function seStatus(sessions,records,grade){
  if(grade)return 'graded';
  if(records.length)return 'submitted';
  if(sessions.some(x=>x.finished))return 'completed';
  if(sessions.length)return 'in_progress';
  return 'not_started';
}
function seLastTime(sessions,records){
  const t=[...sessions.map(x=>x.finished?x.finished_at:x.started_at),...records.map(x=>x.created_at)].filter(Boolean).sort();
  return t.length?t[t.length-1]:null;
}
function seReversal(sessions,records){
  if(sessions.some(x=>x.finished))return true;
  const r=records[0];
  if(r?.readings)return ['V1','V2','V3','V4'].every(k=>Number.isFinite(r.readings[k]));
  return false;
}
function representV0(sessionId,report){
  const m=db.prepare('SELECT payload FROM sim_measurements WHERE session_id=? ORDER BY id LIMIT 1').get(sessionId);
  if(m){const p=parse(m.payload,{});if(Number.isFinite(p.V0_mV))return p.V0_mV;}
  return report.params?report.params.r0*report.params.IS_mA:null;
}
function seKey(sessions,records){
  const done=sessions.find(x=>x.finished);
  if(done){const report=parse(done.state,{}).report;
    if(report)return {idealVH_mV:report.afterCorrection_mV,V0_mV:representV0(done.id,report),VE_mV:report.residualVE_mV,corrected_mV:report.correctedHall_mV};}
  const r=records[0];
  if(r?.result)return {idealVH_mV:r.result.idealVH_mV,V0_mV:r.params?r.params.r0*r.params.I_mA:null,VE_mV:r.result.residualVE_mV,corrected_mV:r.result.correctedHall_mV};
  const cur=sessions[0];
  if(cur){const st=parse(cur.state,{});return {idealVH_mV:st.VH_mV,V0_mV:st.V0_mV,VE_mV:st.VE_mV,corrected_mV:null};}
  return null;
}
function seSessions(studentId){
  return db.prepare("SELECT * FROM sim_sessions WHERE student_id=? AND case_id='case-side-effects' AND demo=0 ORDER BY id DESC").all(studentId)
    .map(x=>({...x,state:parse(x.state,{}),config:parse(x.config,{}),
      measurements:db.prepare('SELECT * FROM sim_measurements WHERE session_id=? ORDER BY id').all(x.id).map(m=>({...m,payload:parse(m.payload,{})})),
      abnormals:db.prepare('SELECT * FROM sim_abnormals WHERE session_id=? ORDER BY id').all(x.id)}));
}
function seRecords(studentId){
  return db.prepare('SELECT * FROM side_effect_records WHERE student_id=? ORDER BY id DESC').all(studentId)
    .map(x=>({id:x.id,title:x.title,conclusion:x.conclusion,created_at:x.created_at,
      params:parse(x.params_json,{}),readings:parse(x.readings_json,{}),result:parse(x.result_json,{})}));
}
r.get('/side-effects',(req,res)=>{
  const rows=students(req).map(s=>{
    const sessions=db.prepare("SELECT id,started_at,finished_at,finished,state FROM sim_sessions WHERE student_id=? AND case_id='case-side-effects' AND demo=0 ORDER BY id DESC").all(s.id);
    const records=seRecords(s.id);
    const grade=db.prepare('SELECT id,total FROM side_effect_grades WHERE student_id=? ORDER BY id DESC LIMIT 1').get(s.id);
    return {id:s.id,name:s.name,student_no:s.student_no,class_id:s.class_id,class_name:s.class_name,avatar:s.avatar,
      status:seStatus(sessions,records,grade),sessionCount:sessions.length,recordCount:records.length,
      lastTime:seLastTime(sessions,records),reversal:seReversal(sessions,records),
      key:seKey(sessions,records),grade:grade?{id:grade.id,total:grade.total}:null};
  });
  res.json(rows);
});
r.get('/side-effects/student/:sid',(req,res)=>{
  const stu=studentAccess(req.user,req.params.sid);
  const sessions=seSessions(stu.id),records=seRecords(stu.id);
  const grades=db.prepare('SELECT * FROM side_effect_grades WHERE student_id=? ORDER BY id DESC').all(stu.id);
  const profile=db.prepare('SELECT id,name,student_no,avatar,class_id FROM users WHERE id=?').get(stu.id);
  const class_name=db.prepare('SELECT name FROM classes WHERE id=?').get(stu.class_id)?.name||null;
  res.json({student:{...profile,class_name},status:seStatus(sessions,records,grades[0]),
    sessions,records,grades});
});
r.post('/side-effects/grade',(req,res)=>{
  const b=req.body;const stu=studentAccess(req.user,b.student_id);
  const sc={};
  for(const [k] of SE_DIMS){
    const v=b.scores?.[k];
    sc[k]=v===null||v===undefined?null:number(v,k,0,10);
  }
  let comment=null;
  if(b.comment!==undefined){if(typeof b.comment!=='string')fail('评语无效');comment=b.comment.trim().slice(0,2000);}
  let total=null;
  if(typeof b.total==='number')total=number(b.total,'总分',0,100);
  else if(SE_DIMS.every(([k])=>sc[k]!==null))total=SE_DIMS.reduce((a,[k])=>a+sc[k],0)/60*100;
  else fail('请完成全部维度评分，或直接填写总分');
  let recordId=null,sessionId=null;
  if(b.record_id){recordId=id(b.record_id);const r=db.prepare('SELECT student_id FROM side_effect_records WHERE id=?').get(recordId);if(!r||r.student_id!==stu.id)fail('记录不属于该学生');}
  if(b.session_id){sessionId=id(b.session_id);const ss=db.prepare("SELECT student_id FROM sim_sessions WHERE id=? AND case_id='case-side-effects'").get(sessionId);if(!ss||ss.student_id!==stu.id)fail('会话不属于该学生');}
  const latest=db.prepare('SELECT * FROM side_effect_grades WHERE student_id=? ORDER BY id DESC LIMIT 1').get(stu.id);
  const now=new Date().toISOString();
  const gradeId=transaction(()=>{
    const info=db.prepare('INSERT INTO side_effect_grades(student_id,record_id,session_id,dim_params,dim_steps,dim_v0,dim_ve,dim_reversal,dim_explain,total,comment,auto,teacher_id,updated_at) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
      .run(stu.id,recordId,sessionId,sc.dim_params,sc.dim_steps,sc.dim_v0,sc.dim_ve,sc.dim_reversal,sc.dim_explain,total,comment,0,req.user.id,now);
    audit(req.user.id,latest?'side-effect-grade-update':'side-effect-grade','side-effect-student-'+stu.id,latest,{id:info.lastInsertRowid,...sc,total,comment});
    return info.lastInsertRowid;
  });
  res.json({ok:1,id:gradeId});
});
module.exports=r;
