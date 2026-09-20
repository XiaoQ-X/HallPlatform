const r=require('express').Router();const {db,fail,parse,text,number,id,transaction,audit,teacher,studentAccess,validFiles}=require('../common');
function itemAccess(user,key){const item=db.prepare('SELECT i.*,u.name student_name,u.class_id FROM review_items i JOIN users u ON u.id=i.student_id WHERE i.id=?').get(id(key));if(!item)fail('作品不存在',404);if(user.role==='teacher')studentAccess(user,item.student_id);else if(item.student_id!==user.id&&(!user.class_id||item.class_id!==user.class_id))fail('只能查看同班作品',403);return item;}
function dimensions(rubric){const dims=parse(rubric.dimensions,[]);if(!Array.isArray(dims)||!dims.length)fail('量表无效');return dims;}
function scoresFor(scores,dims){if(!scores||typeof scores!=='object'||Array.isArray(scores)||Object.keys(scores).length!==dims.length)fail('请完成全部评分维度');for(const d of dims)number(scores[d.name],d.name,0,d.max);if(Object.keys(scores).some(k=>!dims.some(d=>d.name===k)))fail('评分维度无效');return scores;}
r.get('/rubrics',(req,res)=>{const owner=req.user.role==='teacher'?req.user.id:db.prepare('SELECT teacher_id FROM classes WHERE id=?').get(req.user.class_id)?.teacher_id;res.json(db.prepare('SELECT * FROM rubrics WHERE archived=0 AND owner_id=? ORDER BY id').all(owner||0).map(x=>({...x,dimensions:parse(x.dimensions,[])})));});
r.post('/assignments/rebalance',(req,res)=>{
  teacher(req);const result=transaction(()=>{let added=0,skipped=0;
    const items=db.prepare('SELECT i.*,u.class_id FROM review_items i JOIN users u ON u.id=i.student_id JOIN classes c ON c.id=u.class_id WHERE c.teacher_id=?').all(req.user.id);
    for(const item of items){if(!item.rubric_id){skipped++;continue;}
      const existing=db.prepare('SELECT reviewer_id FROM review_assignments WHERE item_id=?').all(item.id);
      const candidates=db.prepare("SELECT u.id,COUNT(a.item_id) load FROM users u LEFT JOIN review_assignments a ON a.reviewer_id=u.id WHERE u.class_id=? AND u.role='student' AND u.active=1 AND u.id<>? GROUP BY u.id ORDER BY load,u.id").all(item.class_id,item.student_id).filter(u=>!existing.some(a=>a.reviewer_id===u.id));
      for(const peer of candidates.slice(0,Math.max(0,2-existing.length))){db.prepare('INSERT INTO review_assignments VALUES(?,?)').run(item.id,peer.id);added++;}
    }audit(req.user.id,'review-assignments','class',null,{added,skipped});return{added,skipped};});res.json(result);
});
r.post('/rubrics',(req,res)=>{teacher(req);const b=req.body;text(b.title,'量表名称',200);if(!['experiment','project'].includes(b.target_type)||!Array.isArray(b.dimensions)||!b.dimensions.length)fail('量表无效');if(new Set(b.dimensions.map(d=>d.name)).size!==b.dimensions.length)fail('维度不能重名');for(const d of b.dimensions){text(d.name,'维度',80);number(d.max,'满分',1,100);text(d.desc,'分档描述',3000);}const info=db.prepare('INSERT INTO rubrics(title,target_type,dimensions,owner_id) VALUES(?,?,?,?)').run(b.title,b.target_type,JSON.stringify(b.dimensions),req.user.id);res.json({id:info.lastInsertRowid});});
r.post('/items',(req,res)=>{
  const b=req.body;const title=text(b.title,'标题',200),summary=text(b.summary,'摘要');if(!['experiment','project'].includes(b.item_type))fail('作品类型无效');
  let recording=null;if(b.recording){recording=validFiles(req.user,[{url:b.recording}])[0].url;const mime=db.prepare('SELECT mime FROM files WHERE id=?').get(recording.split('/').pop()).mime;if(!mime.startsWith('video/'))fail('操作录屏必须为MP4或WebM视频');}
  const files=validFiles(req.user,b.files||[]),rubric=db.prepare('SELECT * FROM rubrics WHERE id=? AND target_type=? AND archived=0').get(id(b.rubric_id),b.item_type);if(!rubric)fail('请选择对应类型量表');
  const instructor=req.user.role==='teacher'?req.user.id:db.prepare('SELECT teacher_id FROM classes WHERE id=?').get(req.user.class_id)?.teacher_id;if(rubric.owner_id!==instructor)fail('量表不属于当前课程',403);
  let evidence,sourceVersion=1;
  if(b.item_type==='experiment'){const session=db.prepare('SELECT id,case_id,started_at,finished_at,config FROM sim_sessions WHERE id=? AND student_id=? AND finished=1 AND demo=0').get(id(b.session_id),req.user.id);if(!session)fail('请选择本人已完成的正式实验');evidence={...session,measurements:db.prepare('SELECT group_id,slot,IS_mA,IM_A,B_T,raw_mV,VH_mV,normVH_mV FROM sim_measurements WHERE session_id=? ORDER BY id').all(session.id)};b.submission_id=null;}
  else{const sub=db.prepare('SELECT id,project_id,version,content,files,snapshot,task_progress,submitted_at FROM project_submissions WHERE id=? AND student_id=?').get(id(b.submission_id),req.user.id);if(!sub)fail('请选择本人课题成果');evidence={...sub,files:parse(sub.files,[]),snapshot:parse(sub.snapshot),task_progress:parse(sub.task_progress,[])};sourceVersion=sub.version;b.session_id=null;}
  const previous=db.prepare("SELECT id FROM review_items WHERE student_id=? AND item_type=? AND COALESCE(session_id,0)=? AND COALESCE(submission_id,0)=? AND source_version=? AND evidence<>'{}'").get(req.user.id,b.item_type,b.session_id||0,b.submission_id||0,sourceVersion);if(previous)return res.json({id:previous.id,reused:true});
  const result=transaction(()=>{
    const info=db.prepare('INSERT INTO review_items(student_id,item_type,title,summary,recording,files,session_id,submission_id,rubric_id) VALUES(?,?,?,?,?,?,?,?,?)').run(req.user.id,b.item_type,title,summary,recording,JSON.stringify(files),b.session_id||null,b.submission_id||null,rubric.id);
    db.prepare('UPDATE review_items SET evidence=?,source_version=? WHERE id=?').run(JSON.stringify(evidence),sourceVersion,info.lastInsertRowid);
    const peers=db.prepare("SELECT u.id,COUNT(a.item_id) load FROM users u LEFT JOIN review_assignments a ON a.reviewer_id=u.id WHERE u.class_id=? AND u.id<>? AND u.role='student' AND u.active=1 GROUP BY u.id ORDER BY load,u.id LIMIT 2").all(req.user.class_id,req.user.id);
    for(const peer of peers)db.prepare('INSERT INTO review_assignments VALUES(?,?)').run(info.lastInsertRowid,peer.id);return info;
  });res.json({id:result.lastInsertRowid});
});
r.get('/items',(req,res)=>{
  const rows=req.user.role==='teacher'?db.prepare('SELECT i.*,u.name student_name FROM review_items i JOIN users u ON u.id=i.student_id JOIN classes c ON c.id=u.class_id WHERE c.teacher_id=? ORDER BY i.id DESC').all(req.user.id):db.prepare('SELECT i.*,u.name student_name FROM review_items i JOIN users u ON u.id=i.student_id JOIN review_assignments a ON a.item_id=i.id WHERE a.reviewer_id=? AND u.class_id=? ORDER BY i.id DESC').all(req.user.id,req.user.class_id);
  res.json(rows.map(x=>({...x,files:parse(x.files,[]),reviewed:!!db.prepare('SELECT id FROM reviews WHERE item_id=? AND reviewer_id=?').get(x.id,req.user.id)})));
});
function reviews(key){return db.prepare('SELECT r.*,u.name reviewer_name FROM reviews r JOIN users u ON u.id=r.reviewer_id WHERE r.item_id=?').all(key).map(x=>({...x,scores:parse(x.scores),dimensions:parse(x.snapshot,[])}));}
r.get('/my-items',(req,res)=>res.json(db.prepare('SELECT * FROM review_items WHERE student_id=? ORDER BY id DESC').all(req.user.id).map(x=>({...x,files:parse(x.files,[]),reviews:reviews(x.id)}))));
r.get('/items/:id',(req,res)=>{const x=itemAccess(req.user,req.params.id),allReviews=reviews(x.id),canCompare=req.user.role==='teacher'||x.student_id===req.user.id||allReviews.some(r=>r.reviewer_id===req.user.id);res.json({...x,files:parse(x.files,[]),evidence:parse(x.evidence),reviews:(canCompare?allReviews:[]).map(rv=>({...rv,mine:rv.reviewer_id===req.user.id}))});});
r.post('/items/:id/review',(req,res)=>{
  const item=itemAccess(req.user,req.params.id);if(item.student_id===req.user.id)fail('不能评价自己的作品');
  if(req.user.role!=='teacher'&&!db.prepare('SELECT 1 FROM review_assignments WHERE item_id=? AND reviewer_id=?').get(item.id,req.user.id))fail('未分配此评价任务',403);
  if(item.rubric_id&&Number(req.body.rubric_id)!==item.rubric_id)fail('请使用作品绑定的量表');
  const rubric=db.prepare('SELECT * FROM rubrics WHERE id=? AND target_type=?').get(id(req.body.rubric_id),item.item_type);if(!rubric)fail('量表类型不匹配');const dims=dimensions(rubric),scores=scoresFor(req.body.scores,dims),comment=text(req.body.comment,'评价依据',5000);
  const exist=db.prepare('SELECT * FROM reviews WHERE item_id=? AND reviewer_id=?').get(item.id,req.user.id);
  if(exist&&db.prepare("SELECT 1 FROM appeals WHERE ref_type='review' AND ref_id=?").get(exist.id))fail('已进入申诉的评价不能修改',409);
  const result=transaction(()=>{
    if(exist){db.prepare('UPDATE reviews SET scores=?,comment=?,snapshot=?,updated_at=CURRENT_TIMESTAMP WHERE id=?').run(JSON.stringify(scores),comment,JSON.stringify(dims),exist.id);audit(req.user.id,'review-update',exist.id,exist,req.body);return exist.id;}
    const info=db.prepare('INSERT INTO reviews(item_id,reviewer_id,rubric_id,scores,comment,snapshot) VALUES(?,?,?,?,?,?)').run(item.id,req.user.id,rubric.id,JSON.stringify(scores),comment,JSON.stringify(dims));db.prepare('INSERT INTO notifications(student_id,title,content,link) VALUES(?,?,?,?)').run(item.student_id,'收到同伴评价',item.title,'/peer/works');return info.lastInsertRowid;
  });res.json({id:result});
});
r.get('/my-reviews',(req,res)=>res.json(db.prepare('SELECT r.*,i.title item_title,u.name owner_name FROM reviews r JOIN review_items i ON i.id=r.item_id JOIN users u ON u.id=i.student_id WHERE reviewer_id=? ORDER BY r.id DESC').all(req.user.id)));
r.post('/appeals',(req,res)=>{
  const b=req.body,content=text(b.content,'申诉理由',10000);if(!['review','grade','general'].includes(b.ref_type))fail('申诉类型无效');let snapshot={};
  if(b.ref_type==='review'){const rv=db.prepare('SELECT * FROM reviews WHERE id=?').get(id(b.ref_id));if(!rv)fail('评价不存在');const item=itemAccess(req.user,rv.item_id);if(item.student_id!==req.user.id)fail('只能申诉本人收到的评价',403);snapshot={review:rv,item};}
  if(b.ref_type==='grade'){const g=db.prepare('SELECT * FROM grade_publications WHERE id=? AND student_id=?').get(id(b.ref_id),req.user.id);if(!g)fail('成绩不存在');snapshot=g;}
  if(b.ref_type!=='general'&&db.prepare("SELECT 1 FROM appeals WHERE ref_type=? AND ref_id=? AND student_id=? AND status<>'已回复'").get(b.ref_type,b.ref_id,req.user.id))fail('该申诉正在处理中');
  const info=db.prepare('INSERT INTO appeals(ref_type,ref_id,student_id,content,snapshot) VALUES(?,?,?,?,?)').run(b.ref_type,b.ref_type==='general'?0:id(b.ref_id),req.user.id,content,JSON.stringify(snapshot));res.json({id:info.lastInsertRowid});
});
r.get('/appeals',(req,res)=>{const rows=req.user.role==='teacher'?db.prepare('SELECT a.*,u.name student_name FROM appeals a JOIN users u ON u.id=a.student_id JOIN classes c ON c.id=u.class_id WHERE c.teacher_id=? ORDER BY a.id DESC').all(req.user.id):db.prepare('SELECT * FROM appeals WHERE student_id=? ORDER BY id DESC').all(req.user.id);res.json(rows.map(x=>({...x,snapshot:parse(x.snapshot)})));});
r.post('/appeals/:id/reply',(req,res)=>{
  teacher(req);const a=db.prepare('SELECT * FROM appeals WHERE id=?').get(id(req.params.id));if(!a)fail('申诉不存在',404);studentAccess(req.user,a.student_id);if(a.status==='已回复')fail('申诉已结案',409);const reply=text(req.body.reply,'复核结论'),decision=req.body.decision||'uphold';if(!['uphold','void'].includes(decision)||decision==='void'&&a.ref_type!=='review')fail('裁决类型无效');
  transaction(()=>{if(decision==='void')db.prepare('UPDATE reviews SET voided=1 WHERE id=?').run(a.ref_id);db.prepare("UPDATE appeals SET reply=?,decision=?,handled_by=?,status='已回复',replied_at=CURRENT_TIMESTAMP WHERE id=?").run(reply,decision,req.user.id,a.id);audit(req.user.id,'appeal-decision',a.id,a,{reply,decision});db.prepare('INSERT INTO notifications(student_id,title,content,link) VALUES(?,?,?,?)').run(a.student_id,'申诉已复核',reply,'/peer/appeals');});res.json({ok:1});
});module.exports=r;
