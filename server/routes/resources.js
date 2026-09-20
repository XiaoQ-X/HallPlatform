const r=require('express').Router();
const {db,fail,parse,text,number,id,transaction,audit,teacher,studentAccess,validFiles}=require('../common');
const {gradeAnswers}=require('../quiz');
const taskHash=p=>require('crypto').createHash('sha256').update(p.tasks||'[]').digest('hex');
const definitions={
  cases:['title','subtitle','cover','color','category','difficulty','tags','content','sim_config','published','sort'],
  ideology:['title','subtitle','cover','color','category','source','content','published','sort'],
  questions:['type','stem','options','answer','analysis','score','tags','published'],
  quizzes:['title','description','time_minutes','question_ids','published'],
  projects:['title','subtitle','cover','color','category','guide','tasks','attachments','published','sort']
};
const jsonKeys=new Set(['tags','sim_config','options','question_ids','tasks','attachments']);
function decode(row,table){if(!row)return row;const result={...row};for(const k of definitions[table])if(jsonKeys.has(k)&&!(k==='tags'&&table==='questions'))result[k]=parse(row[k],['sim_config','options'].includes(k)?null:[]);return result;}
function owner(user){return user.role==='teacher'?user.id:db.prepare('SELECT teacher_id FROM classes WHERE id=?').get(user.class_id)?.teacher_id;}
function resource(table,key,user){const row=db.prepare(`SELECT * FROM ${table} WHERE id=?`).get(id(key));if(!row||row.archived||row.owner_id!==owner(user)||(user.role!=='teacher'&&!row.published))fail('资源不存在或未发布',404);return row;}
function questions(q){const ids=parse(q.question_ids,[]);if(!Array.isArray(ids)||!ids.length)fail('试卷至少需要一道题');const rows=ids.map(key=>db.prepare('SELECT * FROM questions WHERE id=? AND archived=0').get(id(key)));if(rows.some(x=>!x))fail('试卷包含无效题目');return rows;}
function validate(table,b,user){
  text(b[table==='questions'?'stem':'title'],'标题/题干',2000);
  if(table==='questions'){
    if(!['single','multiple','judge','fill','essay'].includes(b.type))fail('题型无效');number(b.score,'分值',.1,100);
    if(b.type!=='essay')text(b.answer,'答案',2000);
    if(['single','multiple'].includes(b.type)){
      if(!Array.isArray(b.options)||b.options.length<2||b.options.length>26||b.options.some(x=>typeof x!=='string'||!x.trim()))fail('选项无效');
      b.answer=b.answer.trim().toUpperCase();const choices=Array.from({length:b.options.length},(_,i)=>String.fromCharCode(65+i));
      if((b.type==='single'&&b.answer.length!==1)||new Set(b.answer).size!==b.answer.length||[...b.answer].some(a=>!choices.includes(a)))fail('正确答案必须对应实际选项，且不可重复');
    }
    if(b.type==='judge'&&!['对','错'].includes(b.answer.trim()))fail('判断题答案必须为对或错');
    if(b.type==='fill'&&b.answer.trim().startsWith('[')){let answers;try{answers=JSON.parse(b.answer);}catch{fail('候选答案JSON无效');}if(!Array.isArray(answers)||!answers.length||answers.some(a=>typeof a!=='string'||!a.trim()))fail('候选答案必须为非空字符串数组');}
  }
  if(table==='quizzes'){number(b.time_minutes,'时长',1,180);if(!Array.isArray(b.question_ids)||!b.question_ids.length||new Set(b.question_ids).size!==b.question_ids.length)fail('题目列表无效');if(questions({question_ids:JSON.stringify(b.question_ids)}).some(q=>q.owner_id!==user.id))fail('题目不属于本人课程',403);}
  if(table==='projects'){if(!Array.isArray(b.tasks)||!b.tasks.length||b.tasks.some(t=>!t.t?.trim()))fail('请填写任务清单');b.attachments=validFiles(user,b.attachments||[]);}
  if(table==='cases'&&b.sim_config){const c=b.sim_config;if(c.maxIs_mA!=null)number(c.maxIs_mA,'工作电流量程',.1,10);if(c.maxIm_A!=null)number(c.maxIm_A,'励磁电流量程',.01,1);}
  return definitions[table].map(k=>{
    let v=b[k];if(k==='published')return v?1:0;if(k==='sort')return Number.isFinite(v)?v:0;
    if(jsonKeys.has(k)&&!(k==='tags'&&table==='questions'))return JSON.stringify(v??(['sim_config','options'].includes(k)?null:[]));
    if(v==null)return null;if(typeof v!=='string'&&typeof v!=='number')fail('字段类型无效');if(typeof v==='string'&&v.length>100000)fail('字段过长');return v;
  });
}
for(const [table,fields]of Object.entries(definitions)){
  r.get('/'+table,(req,res)=>{
    if(table==='questions')teacher(req);
    const rows=db.prepare(`SELECT * FROM ${table} WHERE archived=0 AND owner_id=? ${req.user.role==='teacher'?'':'AND published=1'} ORDER BY id`).all(owner(req.user)||0);res.json(rows.map(x=>decode(x,table)));
  });
  r.post('/'+table,(req,res)=>{teacher(req);const values=validate(table,req.body,req.user);const result=transaction(()=>{const info=db.prepare(`INSERT INTO ${table}(${fields.join(',')},owner_id) VALUES(${fields.map(()=>'?').join(',')},?)`).run(...values,req.user.id);audit(req.user.id,'resource-create',table+':'+info.lastInsertRowid,null,req.body);return info;});res.json({id:result.lastInsertRowid});});
  r.put('/'+table+'/:id',(req,res)=>{teacher(req);const old=resource(table,req.params.id,req.user);if(old.owner_id!==req.user.id)fail('只能编辑本人资源',403);const values=validate(table,req.body,req.user);transaction(()=>{db.prepare(`UPDATE ${table} SET ${fields.map(k=>k+'=?').join(',')} WHERE id=?`).run(...values,old.id);audit(req.user.id,'resource-update',table+':'+old.id,old,req.body);});res.json({ok:1});});
  r.delete('/'+table+'/:id',(req,res)=>{teacher(req);const old=resource(table,req.params.id,req.user);if(old.owner_id!==req.user.id)fail('只能归档本人资源',403);if(table==='questions'&&db.prepare('SELECT question_ids FROM quizzes WHERE archived=0 AND published=1 AND owner_id=?').all(req.user.id).some(q=>parse(q.question_ids,[]).includes(old.id)))fail('此题被已发布试卷引用，请先修改或下架试卷',409);transaction(()=>{db.prepare(`UPDATE ${table} SET archived=1,published=0 WHERE id=?`).run(old.id);audit(req.user.id,'resource-archive',table+':'+old.id,old,null);});res.json({ok:1});});
}
for(const table of ['cases','ideology','projects'])r.get('/'+table+'/:id',(req,res)=>{
  const row=resource(table,req.params.id,req.user),out=decode(row,table);
  if(table==='cases'||table==='ideology')db.prepare(`UPDATE ${table} SET views=views+1 WHERE id=?`).run(row.id);
  if(table==='projects'){
    const mine=db.prepare('SELECT * FROM project_submissions WHERE project_id=? AND student_id=?').get(row.id,req.user.id);out.mine=mine?{...mine,files:parse(mine.files,[])}:null;
    const progress=db.prepare('SELECT * FROM project_progress WHERE student_id=? AND project_id=?').get(req.user.id,row.id);out.progress=progress?.template_hash===taskHash(row)?parse(progress.progress,[]):[];
  }res.json(out);
});
r.get('/quizzes/:id',(req,res)=>{const q=resource('quizzes',req.params.id,req.user);res.json({...q,questions:questions(q).map(({id,type,stem,options,score})=>({id,type,stem,options:parse(options,null),score}))});});
r.post('/quizzes/:id/start',(req,res)=>{
  const q=resource('quizzes',req.params.id,req.user);
  let attempt=db.prepare("SELECT * FROM quiz_attempts WHERE quiz_id=? AND student_id=? AND status='started' ORDER BY id DESC LIMIT 1").get(q.id,req.user.id);
  if(attempt&&new Date(attempt.deadline).getTime()<Date.now()){db.prepare("UPDATE quiz_attempts SET status='expired' WHERE id=?").run(attempt.id);attempt=null;}
  if(!attempt){const snapshot=questions(q);const start=new Date(),deadline=new Date(start.getTime()+q.time_minutes*60000);const info=db.prepare("INSERT INTO quiz_attempts(quiz_id,student_id,answers,score,total,started_at,snapshot,detail,status,deadline) VALUES(?,?,?,0,?,?,?,'[]','started',?)").run(q.id,req.user.id,'{}',snapshot.reduce((a,x)=>a+Number(x.score),0),start.toISOString(),JSON.stringify(snapshot),deadline.toISOString());attempt=db.prepare('SELECT * FROM quiz_attempts WHERE id=?').get(info.lastInsertRowid);}
  res.json({attemptId:attempt.id,deadline:attempt.deadline,answers:parse(attempt.answers),questions:parse(attempt.snapshot,[]).map(({answer,analysis,...q})=>({...q,options:parse(q.options,null)}))});
});
r.get('/attempts/:id/resume',(req,res)=>{const a=db.prepare("SELECT a.*,q.title FROM quiz_attempts a JOIN quizzes q ON q.id=a.quiz_id WHERE a.id=? AND a.student_id=? AND a.status='started'").get(id(req.params.id),req.user.id);if(!a)fail('答卷不可继续',409);res.json({attemptId:a.id,quiz_id:a.quiz_id,title:a.title,deadline:a.deadline,answers:parse(a.answers),questions:parse(a.snapshot,[]).map(({answer,analysis,...q})=>({...q,options:parse(q.options,null)}))});});
r.put('/attempts/:id/draft',(req,res)=>{const a=db.prepare("SELECT * FROM quiz_attempts WHERE id=? AND student_id=? AND status='started'").get(id(req.params.id),req.user.id);if(!a)fail('答卷不可编辑',409);if(Date.now()>Date.parse(a.deadline))fail('答题已到时',409);if(!req.body.answers||typeof req.body.answers!=='object'||Array.isArray(req.body.answers))fail('答案格式错误');db.prepare('UPDATE quiz_attempts SET answers=? WHERE id=?').run(JSON.stringify(req.body.answers),a.id);res.json({ok:1});});
r.post('/quizzes/:id/attempt',(req,res)=>{
  const a=db.prepare('SELECT * FROM quiz_attempts WHERE id=? AND quiz_id=? AND student_id=?').get(id(req.body.attemptId),id(req.params.id),req.user.id);if(!a)fail('请先开始答题',409);
  if(a.status==='expired')fail('本次答卷已过期',409);
  if(a.status!=='started')return res.json({attemptId:a.id,score:a.score,total:a.total,status:a.status,detail:parse(a.detail,[]),answers:parse(a.answers)});
  const late=Date.now()>Date.parse(a.deadline);
  const answers=late?parse(a.answers):req.body.answers;
  if(!answers||typeof answers!=='object'||Array.isArray(answers))fail('答案格式错误');
  const result=gradeAnswers(parse(a.snapshot,[]),answers);
  db.prepare('UPDATE quiz_attempts SET answers=?,score=?,total=?,detail=?,status=?,submitted_at=? WHERE id=?').run(JSON.stringify(answers),result.score,result.total,JSON.stringify(result.detail),result.status,new Date().toISOString(),a.id);
  res.json({attemptId:a.id,...result,answers,usedSavedDraft:late});
});
r.get('/attempts',(req,res)=>{
  const rows=req.user.role==='teacher'?db.prepare('SELECT a.*,u.name student_name,q.title quiz_title FROM quiz_attempts a JOIN users u ON u.id=a.student_id JOIN classes c ON c.id=u.class_id JOIN quizzes q ON q.id=a.quiz_id WHERE c.teacher_id=? ORDER BY a.id DESC').all(req.user.id):db.prepare('SELECT a.*,q.title quiz_title FROM quiz_attempts a JOIN quizzes q ON q.id=a.quiz_id WHERE a.student_id=? ORDER BY a.id DESC').all(req.user.id);
  res.json(rows.map(a=>!['pending','graded'].includes(a.status)?(({snapshot,detail,...x})=>x)(a):a));
});
r.post('/attempts/:id/grade',(req,res)=>{teacher(req);const a=db.prepare('SELECT * FROM quiz_attempts WHERE id=?').get(id(req.params.id));if(!a||!['pending','graded'].includes(a.status))fail('答卷不可评阅');studentAccess(req.user,a.student_id);const details=parse(a.detail,[]),essays=new Set(parse(a.snapshot,[]).filter(q=>q.type==='essay').map(q=>q.id));if(!essays.size)fail('此答卷没有简答题');for(const d of details)if(essays.has(d.id)){d.score=number(req.body.scores?.[d.id],'简答题分数',0,d.max);d.pending=false;d.feedback=text(req.body.feedback,'反馈');}transaction(()=>{db.prepare("UPDATE quiz_attempts SET score=?,detail=?,status='graded' WHERE id=?").run(details.reduce((s,d)=>s+d.score,0),JSON.stringify(details),a.id);audit(req.user.id,'quiz-grade',a.id,a,details);db.prepare('INSERT INTO notifications(student_id,title,content,link) VALUES(?,?,?,?)').run(a.student_id,'答卷已评阅',req.body.feedback,'/resources/quiz?id='+a.quiz_id);});res.json({ok:1});});

r.post('/grade-submission/:id',(req,res,next)=>{teacher(req);const s=db.prepare('SELECT * FROM project_submissions WHERE id=?').get(id(req.params.id));if(s){studentAccess(req.user,s.student_id);if(req.body.version!=null&&req.body.version!==s.version)fail('报告已有新版本，请重新打开报告后评阅',409);}next();});
r.put('/projects/:id/progress',(req,res)=>{const p=resource('projects',req.params.id,req.user);if(!Array.isArray(req.body.progress)||req.body.progress.length!==parse(p.tasks,[]).length||req.body.progress.some(v=>typeof v!=='boolean'))fail('进度无效');db.prepare('INSERT INTO project_progress(student_id,project_id,progress,template_hash) VALUES(?,?,?,?) ON CONFLICT(student_id,project_id) DO UPDATE SET progress=excluded.progress,template_hash=excluded.template_hash').run(req.user.id,p.id,JSON.stringify(req.body.progress),taskHash(p));res.json({ok:1});});
r.post('/projects/:id/submit',(req,res)=>{
  const p=resource('projects',req.params.id,req.user),content=text(req.body.content,'报告内容',100000),files=validFiles(req.user,req.body.files||[]);
  const exist=db.prepare('SELECT * FROM project_submissions WHERE project_id=? AND student_id=?').get(p.id,req.user.id);
  const progress=db.prepare('SELECT * FROM project_progress WHERE student_id=? AND project_id=?').get(req.user.id,p.id);const taskProgress=progress?.template_hash===taskHash(p)?progress.progress:'[]';
  if(exist?.grade!=null)fail('已评分报告已锁定，请联系教师退回后重交',409);
  const result=transaction(()=>{
    if(exist){db.prepare('INSERT INTO submission_versions(submission_id,version,payload) VALUES(?,?,?)').run(exist.id,exist.version,JSON.stringify(exist));db.prepare("UPDATE project_submissions SET content=?,files=?,version=version+1,snapshot=?,task_progress=?,feedback=NULL,submitted_at=CURRENT_TIMESTAMP,graded_at=NULL WHERE id=?").run(content,JSON.stringify(files),JSON.stringify(p),taskProgress,exist.id);return exist.id;}
    return db.prepare('INSERT INTO project_submissions(project_id,student_id,content,files,submitted_at,snapshot,task_progress) VALUES(?,?,?,?,CURRENT_TIMESTAMP,?,?)').run(p.id,req.user.id,content,JSON.stringify(files),JSON.stringify(p),taskProgress).lastInsertRowid;
  });res.json({id:result});
});
for(const mine of [true,false])r.get(mine?'/my-submissions':'/submissions',(req,res)=>{
  if(!mine)teacher(req);let rows=db.prepare(`SELECT s.*,u.name student_name,p.title project_title FROM project_submissions s JOIN users u ON u.id=s.student_id JOIN projects p ON p.id=s.project_id ${mine?'WHERE s.student_id=?':'JOIN classes c ON c.id=u.class_id WHERE c.teacher_id=?'} ORDER BY s.id DESC`).all(req.user.id);
  if(req.query.project_id)rows=rows.filter(x=>x.project_id===Number(req.query.project_id));res.json(rows.map(x=>({...x,files:parse(x.files,[]),snapshot:parse(x.snapshot)})));
});
r.post('/grade-submission/:id',(req,res)=>{teacher(req);const sub=db.prepare('SELECT * FROM project_submissions WHERE id=?').get(id(req.params.id));if(!sub)fail('提交不存在',404);studentAccess(req.user,sub.student_id);const g=number(req.body.grade,'成绩',0,100),feedback=text(req.body.feedback,'反馈');transaction(()=>{db.prepare('UPDATE project_submissions SET grade=?,feedback=?,graded_at=CURRENT_TIMESTAMP WHERE id=?').run(g,feedback,sub.id);audit(req.user.id,'project-grade',sub.id,sub,{grade:g,feedback});db.prepare('INSERT INTO notifications(student_id,title,content,link) VALUES(?,?,?,?)').run(sub.student_id,'课题成绩已发布',feedback,'/resources/projects?id='+sub.project_id);});res.json({ok:1});});
r.get('/submissions/:id/versions',(req,res)=>{const sub=db.prepare('SELECT * FROM project_submissions WHERE id=?').get(id(req.params.id));if(!sub)fail('提交不存在',404);studentAccess(req.user,sub.student_id);res.json(db.prepare('SELECT * FROM submission_versions WHERE submission_id=? ORDER BY id DESC').all(sub.id).map(v=>({...v,payload:parse(v.payload)})));});
r.post('/submissions/:id/reopen',(req,res)=>{teacher(req);const sub=db.prepare('SELECT * FROM project_submissions WHERE id=?').get(id(req.params.id));if(!sub)fail('提交不存在',404);studentAccess(req.user,sub.student_id);const reason=text(req.body.reason,'退回理由');if(sub.grade==null)fail('该报告已处于待评/退回状态',409);transaction(()=>{db.prepare('INSERT INTO submission_versions(submission_id,version,payload) VALUES(?,?,?)').run(sub.id,sub.version,JSON.stringify(sub));db.prepare('UPDATE project_submissions SET grade=NULL,feedback=?,graded_at=NULL WHERE id=?').run(reason,sub.id);audit(req.user.id,'project-reopen',sub.id,sub,{reason});db.prepare('INSERT INTO notifications(student_id,title,content,link) VALUES(?,?,?,?)').run(sub.student_id,'课题退回重交',reason,'/resources/projects?id='+sub.project_id);});res.json({ok:1});});
module.exports=r;
