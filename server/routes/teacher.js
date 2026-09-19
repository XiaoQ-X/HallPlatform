module.exports = (() => {
  const r = require('express').Router();
  const db = require('../db');
  const bcrypt = require('bcryptjs');
  const safe = s => { try { return JSON.parse(s); } catch { return s; } };
  const r1 = v => Math.round(v * 10) / 10;

  r.get('/dashboard', (req, res) => {
    const studentCount = db.prepare("SELECT COUNT(*) c FROM users WHERE role='student'").get().c;
    const classCount = db.prepare('SELECT COUNT(*) c FROM classes').get().c;
    const sessionCount = db.prepare('SELECT COUNT(*) c FROM sim_sessions').get().c;
    const finishedCount = db.prepare('SELECT COUNT(*) c FROM sim_sessions WHERE finished=1').get().c;
    const subCount = db.prepare('SELECT COUNT(*) c FROM project_submissions').get().c;
    const gradedCount = db.prepare('SELECT COUNT(*) c FROM project_submissions WHERE grade IS NOT NULL').get().c;
    const abnormalDist = db.prepare(`SELECT code, COUNT(*) count FROM sim_abnormals GROUP BY code ORDER BY count DESC`).all();
    const quizAvg = db.prepare('SELECT AVG(score*100.0/total) a FROM quiz_attempts').get().a;
    const projectAvg = db.prepare('SELECT AVG(grade) a FROM project_submissions WHERE grade IS NOT NULL').get().a;
    const dailySessions = db.prepare(`SELECT date(started_at) d, COUNT(*) c FROM sim_sessions
      GROUP BY date(started_at) ORDER BY d DESC LIMIT 7`).all().reverse();
    const resourceCount = {
      cases: db.prepare('SELECT COUNT(*) c FROM cases WHERE published=1').get().c,
      ideology: db.prepare('SELECT COUNT(*) c FROM ideology WHERE published=1').get().c,
      questions: db.prepare('SELECT COUNT(*) c FROM questions WHERE published=1').get().c,
      projects: db.prepare('SELECT COUNT(*) c FROM projects WHERE published=1').get().c
    };
    res.json({
      studentCount, classCount, sessionCount, finishedCount,
      completionRate: sessionCount ? r1(finishedCount * 100 / sessionCount) : 0,
      subCount, gradedCount, quizAvg: r1(quizAvg || 0), projectAvg: r1(projectAvg || 0),
      abnormalDist, dailySessions, resourceCount
    });
  });

  r.get('/students', (req, res) => {
    const students = db.prepare(`SELECT u.*, c.name class_name FROM users u
      LEFT JOIN classes c ON c.id=u.class_id WHERE u.role='student' ORDER BY u.id`).all();
    res.json(students.map(s => {
      const sessions = db.prepare('SELECT COUNT(*) c FROM sim_sessions WHERE student_id=?').get(s.id).c;
      const finished = db.prepare('SELECT COUNT(*) c FROM sim_sessions WHERE student_id=? AND finished=1').get(s.id).c;
      const errors = db.prepare(`SELECT COUNT(*) c FROM sim_abnormals a
        JOIN sim_sessions x ON x.id=a.session_id WHERE x.student_id=?`).get(s.id).c;
      const subs = db.prepare('SELECT COUNT(*) c FROM project_submissions WHERE student_id=?').get(s.id).c;
      const graded = db.prepare('SELECT AVG(grade) a FROM project_submissions WHERE student_id=? AND grade IS NOT NULL').get(s.id).a;
      const quiz = db.prepare('SELECT MAX(score*100.0/total) a FROM quiz_attempts WHERE student_id=?').get(s.id).a;
      return { ...s, sessions, finished, errors, subs, projectAvg: r1(graded), quizBest: r1(quiz) };
    }));
  });

  r.get('/grades', (req, res) => {
    const students = db.prepare(`SELECT u.*, c.name class_name FROM users u
      LEFT JOIN classes c ON c.id=u.class_id WHERE u.role='student' ORDER BY u.id`).all();
    const grades = students.map(s => {
      const finished = db.prepare('SELECT COUNT(*) c FROM sim_sessions WHERE student_id=? AND finished=1').get(s.id).c;
      const errors = db.prepare(`SELECT COUNT(*) c FROM sim_abnormals a
        JOIN sim_sessions x ON x.id=a.session_id WHERE x.student_id=?`).get(s.id).c;
      const simScore = finished ? r1(Math.max(60, Math.min(100, 95 - errors * 4))) : 0;
      const quizBest = db.prepare('SELECT MAX(score*100.0/total) a FROM quiz_attempts WHERE student_id=?').get(s.id).a;
      const projectAvg = db.prepare('SELECT AVG(grade) a FROM project_submissions WHERE student_id=? AND grade IS NOT NULL').get(s.id).a;
      const reviews = db.prepare(`SELECT rv.scores, rub.dimensions FROM reviews rv
        JOIN review_items i ON i.id=rv.item_id LEFT JOIN rubrics rub ON rub.id=rv.rubric_id
        WHERE i.student_id=?`).all(s.id);
      let peerPct = null;
      if (reviews.length) {
        const pcts = reviews.map(rv => {
          const scores = safe(rv.scores), dims = safe(rv.dimensions);
          if (!Array.isArray(dims)) return null;
          const got = dims.reduce((a, d) => a + (scores[d.name] || 0), 0);
          const max = dims.reduce((a, d) => a + d.max, 0);
          return got * 100 / max;
        }).filter(x => x != null);
        if (pcts.length) peerPct = r1(pcts.reduce((a, b) => a + b, 0) / pcts.length);
      }
      const gave = db.prepare(`SELECT COUNT(*) c FROM reviews WHERE reviewer_id=?`).get(s.id).c;
      if (peerPct == null) peerPct = gave ? r1(Math.min(90, gave * 30 + 60)) : 0;
      const comp = {
        sim: { score: simScore, weight: 0.3 },
        quiz: { score: r1(quizBest || 0), weight: 0.25 },
        project: { score: r1(projectAvg || 0), weight: 0.3 },
        peer: { score: peerPct, weight: 0.15 }
      };
      db.prepare('SELECT component,score FROM grade_overrides WHERE student_id=?').all(s.id)
        .forEach(o => { if (comp[o.component]) { comp[o.component].score = r1(o.score); comp[o.component].overridden = true; } });
      const total = r1(Object.values(comp).reduce((a, c) => a + c.score * c.weight, 0));
      return { id: s.id, name: s.name, student_no: s.student_no, class_name: s.class_name, comp, total };
    });
    res.json(grades);
  });

  r.get('/pushes', (req, res) => {
    res.json(db.prepare(`SELECT p.*, c.name target_name FROM pushes p
      LEFT JOIN classes c ON c.id=p.target_id AND p.target_type='class' ORDER BY p.id DESC`).all());
  });
  r.post('/pushes', (req, res) => {
    const b = req.body;
    const linkMap = { case:'/resources/cases', ideology:'/resources/ideology',
      quiz:'/resources/quiz', project:'/resources/projects' };
    const link = b.link || (b.resource_type ? linkMap[b.resource_type] : '');
    const info = db.prepare(`INSERT INTO pushes(teacher_id,target_type,target_id,title,message,resource_type,resource_id)
      VALUES(?,?,?,?,?,?,?)`).run(req.user.id, b.target_type, b.target_id, b.title, b.message,
      b.resource_type, b.resource_id);
    if (b.target_type === 'class') {
      const ids = db.prepare('SELECT id FROM users WHERE class_id=? AND role=?').all(b.target_id, 'student');
      ids.forEach(x => db.prepare('INSERT INTO notifications(student_id,title,content,link) VALUES(?,?,?,?)')
        .run(x.id, b.title, b.message, link));
    } else if (b.target_type === 'student') {
      db.prepare('INSERT INTO notifications(student_id,title,content,link) VALUES(?,?,?,?)')
        .run(b.target_id, b.title, b.message, link);
    }
    res.json({ id: info.lastInsertRowid });
  });

  // ---------- 班级管理 ----------
  r.post('/classes', (req, res) => {
    const { name, code } = req.body;
    if (!name) return res.status(400).json({ error: '请填写班级名称' });
    if (db.prepare('SELECT id FROM classes WHERE name=?').get(name))
      return res.status(400).json({ error: '班级已存在' });
    const info = db.prepare('INSERT INTO classes(name,code) VALUES(?,?)').run(name, code || '');
    res.json({ id: info.lastInsertRowid });
  });

  // ---------- 学生账号管理 ----------
  r.post('/students', (req, res) => {
    const b = req.body;
    if (!b.username || !b.password || !b.name)
      return res.status(400).json({ error: '账号、初始密码、姓名均为必填' });
    if (db.prepare('SELECT id FROM users WHERE username=?').get(b.username))
      return res.status(400).json({ error: '账号已存在' });
    if (b.student_no && db.prepare('SELECT id FROM users WHERE student_no=? AND class_id=?').get(b.student_no, b.class_id))
      return res.status(400).json({ error: '该班级中学号已存在' });
    const hash = bcrypt.hashSync(b.password, 8);
    const info = db.prepare('INSERT INTO users(username,password,name,role,class_id,student_no,avatar) VALUES(?,?,?,?,?,?,?)')
      .run(b.username, hash, b.name, 'student', b.class_id || null, b.student_no || '', b.avatar || '🧑‍🎓');
    res.json({ id: info.lastInsertRowid });
  });
  r.put('/students/:id', (req, res) => {
    const b = req.body;
    db.prepare('UPDATE users SET name=?,class_id=?,student_no=?,avatar=? WHERE id=? AND role=\'student\'')
      .run(b.name, b.class_id || null, b.student_no || '', b.avatar || '🧑‍🎓', req.params.id);
    res.json({ ok: 1 });
  });
  r.post('/students/:id/reset-password', (req, res) => {
    const { password } = req.body;
    if (!password) return res.status(400).json({ error: '请提供新密码' });
    const hash = bcrypt.hashSync(password, 8);
    db.prepare('UPDATE users SET password=? WHERE id=? AND role=\'student\'').run(hash, req.params.id);
    res.json({ ok: 1 });
  });

  // ---------- 成绩手动调整 ----------
  r.post('/grades/override', (req, res) => {
    const { student_id, component, score } = req.body;
    if (!['sim','quiz','project','peer'].includes(component))
      return res.status(400).json({ error: '组件类型无效' });
    const v = Number(score);
    if (Number.isNaN(v) || v < 0 || v > 100) return res.status(400).json({ error: '分数须为 0-100' });
    const exist = db.prepare('SELECT id FROM grade_overrides WHERE student_id=? AND component=?').get(student_id, component);
    if (exist) db.prepare('UPDATE grade_overrides SET score=? WHERE id=?').run(v, exist.id);
    else db.prepare('INSERT INTO grade_overrides(student_id,component,score,max_score) VALUES(?,?,?,100)')
      .run(student_id, component, v);
    res.json({ ok: 1 });
  });
  r.delete('/grades/override', (req, res) => {
    const { student_id, component } = req.body;
    db.prepare('DELETE FROM grade_overrides WHERE student_id=? AND component=?').run(student_id, component);
    res.json({ ok: 1 });
  });

  return r;
})();
