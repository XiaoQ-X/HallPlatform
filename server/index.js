const express = require('express');
const jwt = require('jsonwebtoken');
const path = require('path');
const fs = require('fs');
if(process.env.HALL_DEMO==='1')require('./seed');
const db = require('./db');
require('./content-migration')(db);
require('./formula-migration')(db);
require('./integrity-migration')(db);
require('./legacy-review-migration')(db);
require('./workflow-migration')(db);

const app = express();
app.use(express.json({ limit: '20mb' }));
app.use(express.urlencoded({ extended: true }));

const SECRET=require('./config').secret;
app.disable('x-powered-by');
app.use((req,res,next)=>{res.setHeader('X-Content-Type-Options','nosniff');res.setHeader('Referrer-Policy','same-origin');next();});

// 静态资源
const distDir = path.join(__dirname, '..', 'dist');
app.use('/sim', express.static(path.join(__dirname, '..', 'public', 'sim')));
// 下载路径必须在 SPA 回退之前明确返回 404。否则一个不存在的安装包会
// 被 index.html 兜底成 200，浏览器会把网页误当成 .exe 下载。
// In production installers live outside the release tree so an application
// release cannot accidentally overwrite or expose them. The directory can be
// overridden for local development; the route still returns a real 404 when
// the package is absent instead of falling through to the SPA.
const downloadDir = process.env.HALL_DOWNLOAD_DIR || path.join(__dirname, '..', 'public', 'downloads');
app.use('/downloads', express.static(downloadDir));
app.use('/downloads', (req,res)=>res.status(404).json({error:'安装包暂未提供'}));
app.use('/uploads', (req,res)=>res.status(410).json({error:'旧附件请通过教师确认后重新上传'}));

// 认证中间件
function auth(req, res, next) {
  const h = req.headers.authorization;
  if (!h) return res.status(401).json({ error: '未登录' });
  try {
    const claims=jwt.verify(h.replace(/^Bearer /,''),SECRET,{algorithms:['HS256']});
    req.user=db.prepare('SELECT id,role,name,class_id,token_version,active,must_change FROM users WHERE id=?').get(claims.id);
    if(!req.user||!req.user.active||req.user.must_change||claims.version!==req.user.token_version)throw Error('失效');
    next();
  } catch { res.status(401).json({ error: '登录已失效' }); }
}
function teacherOnly(req, res, next) {
  if (req.user.role !== 'teacher') return res.status(403).json({ error: '仅教师可操作' });
  next();
}

app.use('/api/auth', require('./routes/auth')(jwt, SECRET, auth));
app.use('/api/resources', auth, require('./routes/resources'));
app.use('/api/sim', auth, require('./routes/sim'));
app.use('/api/workshop', auth, require('./routes/workshop'));
app.use('/api/peer', auth, require('./routes/peer'));
app.use('/api/teacher', auth, teacherOnly, require('./routes/teacher'));
app.use('/api/upload', auth, require('./routes/upload'));
app.use('/api/files', auth, require('./routes/files'));
app.use('/api/grades',auth,require('./routes/grades'));
app.get('/api/notifications', auth, (req, res) => {
  res.json(db.prepare('SELECT * FROM notifications WHERE student_id=? AND id<? ORDER BY id DESC LIMIT 50').all(req.user.id,Number(req.query.before)||Number.MAX_SAFE_INTEGER));
});
app.get('/api/notifications-count',auth,(req,res)=>res.json(db.prepare('SELECT COUNT(*) unread FROM notifications WHERE student_id=? AND read=0').get(req.user.id)));
app.put('/api/notifications/:id/read', auth, (req, res) => {
  db.prepare('UPDATE notifications SET read=1 WHERE id=? AND student_id=?').run(req.params.id, req.user.id);
  res.json({ ok: 1 });
});
app.put('/api/notifications/read-all', auth, (req, res) => {
  db.prepare('UPDATE notifications SET read=1 WHERE student_id=?').run(req.user.id);
  res.json({ ok: 1 });
});
app.use('/api/me', auth, (req,res)=>{
  const db=require('./db');
  const u=db.prepare('SELECT id,username,name,role,class_id,student_no,avatar FROM users WHERE id=?').get(req.user.id);
  let className=null;
  if(u.class_id){ className=db.prepare('SELECT name FROM classes WHERE id=?').get(u.class_id)?.name; }
  const presentation=require('./presentation-identity');
  res.json(presentation.visibleProfile(db,{...u, class_name:className}));
});

app.use('/api',(req,res)=>res.status(404).json({error:'接口不存在'}));
app.use((err,req,res,next)=>{console.error(err.message);res.status(err.status||400).json({error:err.status?err.message:'请求无法完成，请检查输入或联系教师'});});
// 前端 SPA（构建后）
if (fs.existsSync(distDir)) {
  app.use(express.static(distDir));
  app.get(/^\/(?!api|uploads).*/, (req, res) => res.sendFile(path.join(distDir, 'index.html')));
}

const PORT = process.env.PORT || 8181;
if(require.main===module)app.listen(PORT,process.env.HOST||'127.0.0.1',()=>console.log(`霍尔效应教学平台已启动：http://127.0.0.1:${PORT}`));
module.exports=app;
