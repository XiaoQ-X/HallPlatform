const r=require('express').Router(),path=require('path');const {db,fail,parse,studentAccess}=require('../common');
function references(s,file){const list=parse(s,[]);return Array.isArray(list)&&list.some(f=>f.id===file.id||f.url==='/api/files/'+file.id);}
r.get('/:id',(req,res)=>{
  const file=db.prepare('SELECT * FROM files WHERE id=?').get(req.params.id);if(!file)fail('文件不存在',404);
  let allowed=file.owner_id===req.user.id;
  if(!allowed&&req.user.role==='teacher'){try{studentAccess(req.user,file.owner_id);allowed=true;}catch{}}
  if(!allowed){const owner=req.user.role==='teacher'?req.user.id:db.prepare('SELECT teacher_id FROM classes WHERE id=?').get(req.user.class_id)?.teacher_id;allowed=db.prepare('SELECT attachments FROM projects WHERE published=1 AND archived=0 AND owner_id=?').all(owner||0).some(p=>references(p.attachments,file));}
  if(!allowed)allowed=db.prepare('SELECT i.*,u.class_id FROM review_items i JOIN users u ON u.id=i.student_id WHERE u.class_id=?').all(req.user.class_id).some(i=>i.recording==='/api/files/'+file.id||references(i.files,file)||references(JSON.stringify(parse(i.evidence).files||[]),file));
  if(!allowed)fail('无权访问附件',403);
  res.setHeader('Content-Type',file.mime);res.setHeader('X-Content-Type-Options','nosniff');res.setHeader('Content-Security-Policy',"default-src 'none'; sandbox");res.setHeader('Cache-Control','private, no-store');
  const inline=/^(video|image)\//.test(file.mime);
  res.setHeader('Content-Disposition',`${inline?'inline':'attachment'}; filename*=UTF-8''${encodeURIComponent(file.name)}`);
  res.sendFile(path.join(process.env.HALL_UPLOAD_DIR||path.join(__dirname,'../../uploads'),file.path));
});module.exports=r;
