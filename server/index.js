const express = require('express');
const jwt = require('jsonwebtoken');
const path = require('path');
const fs = require('fs');
require('./seed');
const db = require('./db');

const app = express();
app.use(express.json({ limit: '20mb' }));
app.use(express.urlencoded({ extended: true }));

const SECRET = 'hall-teaching-platform-secret-2026';

// 静态资源
const distDir = path.join(__dirname, '..', 'dist');
app.use('/sim', express.static(path.join(__dirname, '..', 'public', 'sim')));
app.use('/uploads', express.static(path.join(__dirname, '..', 'uploads')));

// 认证中间件
function auth(req, res, next) {
  const h = req.headers.authorization;
  if (!h) return res.status(401).json({ error: '未登录' });
  try {
    req.user = jwt.verify(h.replace('Bearer ', ''), SECRET);
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
app.get('/api/notifications', auth, (req, res) => {
  res.json(db.prepare('SELECT * FROM notifications WHERE student_id=? ORDER BY id DESC LIMIT 30').all(req.user.id));
});
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
  res.json({...u, class_name:className});
});

// 前端 SPA（构建后）
if (fs.existsSync(distDir)) {
  app.use(express.static(distDir));
  app.get(/^\/(?!api|uploads).*/, (req, res) => res.sendFile(path.join(distDir, 'index.html')));
}

const PORT = process.env.PORT || 8181;
app.listen(PORT, () => console.log(`霍尔效应教学平台已启动：http://127.0.0.1:${PORT}`));
