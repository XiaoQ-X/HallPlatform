module.exports = (() => {
  const r = require('express').Router();
  const db = require('../db');
  const isT = req => req.user.role === 'teacher';
  const parse = (s, d) => { try { return JSON.parse(s); } catch { return d; } };
  const pub = t => isT ? {} : { published: 1 };

  // ---------- 案例库 ----------
  r.get('/cases', (req, res) => {
    const rows = db.prepare('SELECT * FROM cases ORDER BY sort,id').all()
      .filter(c => isT(req) || c.published);
    res.json(rows.map(c => ({ ...c, tags: parse(c.tags, []), sim_config: parse(c.sim_config, null) })));
  });
  r.get('/cases/:id', (req, res) => {
    const c = db.prepare('SELECT * FROM cases WHERE id=?').get(req.params.id);
    if (!c || (!c.published && !isT(req))) return res.status(404).json({ error: '未找到' });
    db.prepare('UPDATE cases SET views=views+1 WHERE id=?').run(c.id);
    res.json({ ...c, tags: parse(c.tags, []), sim_config: parse(c.sim_config, null) });
  });
  r.post('/cases', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    const b = req.body;
    const info = db.prepare(`INSERT INTO cases(title,subtitle,cover,color,category,difficulty,tags,content,sim_config,published,sort)
      VALUES(?,?,?,?,?,?,?,?,?,?,?)`).run(b.title, b.subtitle, b.cover || '📘', b.color || '#0EA5E9',
      b.category, b.difficulty, JSON.stringify(b.tags || []), b.content,
      b.sim_config ? JSON.stringify(b.sim_config) : null, b.published ? 1 : 0, b.sort || 0);
    res.json({ id: info.lastInsertRowid });
  });
  r.put('/cases/:id', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    const b = req.body;
    db.prepare(`UPDATE cases SET title=?,subtitle=?,cover=?,color=?,category=?,difficulty=?,tags=?,content=?,sim_config=?,published=?,updated_at=datetime('now','localtime') WHERE id=?`)
      .run(b.title, b.subtitle, b.cover, b.color, b.category, b.difficulty,
        JSON.stringify(b.tags || []), b.content, b.sim_config ? JSON.stringify(b.sim_config) : null,
        b.published ? 1 : 0, req.params.id);
    res.json({ ok: 1 });
  });
  r.delete('/cases/:id', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    db.prepare('DELETE FROM cases WHERE id=?').run(req.params.id); res.json({ ok: 1 });
  });

  // ---------- 思政库 ----------
  r.get('/ideology', (req, res) => {
    const rows = db.prepare('SELECT * FROM ideology ORDER BY sort,id').all()
      .filter(c => isT(req) || c.published);
    res.json(rows.map(c => ({ ...c })));
  });
  r.get('/ideology/:id', (req, res) => {
    const c = db.prepare('SELECT * FROM ideology WHERE id=?').get(req.params.id);
    if (!c || (!c.published && !isT(req))) return res.status(404).json({ error: '未找到' });
    db.prepare('UPDATE ideology SET views=views+1 WHERE id=?').run(c.id);
    res.json(c);
  });
  r.post('/ideology', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    const b = req.body;
    const info = db.prepare(`INSERT INTO ideology(title,subtitle,cover,color,category,source,content,published,sort)
      VALUES(?,?,?,?,?,?,?,?,?)`).run(b.title, b.subtitle, b.cover || '🌟', b.color || '#0284C7',
      b.category, b.source, b.content, b.published ? 1 : 0, b.sort || 0);
    res.json({ id: info.lastInsertRowid });
  });
  r.put('/ideology/:id', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    const b = req.body;
    db.prepare(`UPDATE ideology SET title=?,subtitle=?,cover=?,color=?,category=?,source=?,content=?,published=? WHERE id=?`)
      .run(b.title, b.subtitle, b.cover, b.color, b.category, b.source, b.content, b.published ? 1 : 0, req.params.id);
    res.json({ ok: 1 });
  });
  r.delete('/ideology/:id', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    db.prepare('DELETE FROM ideology WHERE id=?').run(req.params.id); res.json({ ok: 1 });
  });

  // ---------- 题库（教师） ----------
  r.get('/questions', (req, res) => {
    const rows = db.prepare('SELECT * FROM questions ORDER BY id').all()
      .filter(q => isT(req) || q.published);
    res.json(rows.map(q => ({ ...q, options: parse(q.options, null) })));
  });
  r.post('/questions', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    const b = req.body;
    const info = db.prepare(`INSERT INTO questions(type,stem,options,answer,analysis,score,tags,published)
      VALUES(?,?,?,?,?,?,?,?)`).run(b.type, b.stem, b.options ? JSON.stringify(b.options) : null,
      b.answer, b.analysis, b.score || 2, b.tags, b.published ? 1 : 0);
    res.json({ id: info.lastInsertRowid });
  });
  r.put('/questions/:id', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    const b = req.body;
    db.prepare(`UPDATE questions SET type=?,stem=?,options=?,answer=?,analysis=?,score=?,tags=?,published=? WHERE id=?`)
      .run(b.type, b.stem, b.options ? JSON.stringify(b.options) : null, b.answer, b.analysis,
        b.score || 2, b.tags, b.published ? 1 : 0, req.params.id);
    res.json({ ok: 1 });
  });
  r.delete('/questions/:id', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    db.prepare('DELETE FROM questions WHERE id=?').run(req.params.id); res.json({ ok: 1 });
  });

  // ---------- 试卷与作答 ----------
  r.get('/quizzes', (req, res) => {
    const rows = db.prepare('SELECT * FROM quizzes ORDER BY id').all()
      .filter(q => isT(req) || q.published);
    res.json(rows.map(q => ({ ...q, question_ids: parse(q.question_ids, []) })));
  });
  r.get('/quizzes/:id', (req, res) => {
    const q = db.prepare('SELECT * FROM quizzes WHERE id=?').get(req.params.id);
    if (!q || (!q.published && !isT(req))) return res.status(404).json({ error: '未找到' });
    const ids = parse(q.question_ids, []);
    const questions = ids.map(id => {
      const x = db.prepare('SELECT id,type,stem,options,score FROM questions WHERE id=?').get(id);
      return x ? { ...x, options: parse(x.options, null) } : null;
    }).filter(Boolean);
    res.json({ ...q, questions });
  });
  r.post('/quizzes', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    const b = req.body;
    const info = db.prepare('INSERT INTO quizzes(title,description,time_minutes,question_ids,published) VALUES(?,?,?,?,?)')
      .run(b.title, b.description, b.time_minutes || 20, JSON.stringify(b.question_ids || []), b.published ? 1 : 0);
    res.json({ id: info.lastInsertRowid });
  });
  r.delete('/quizzes/:id', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    db.prepare('DELETE FROM quizzes WHERE id=?').run(req.params.id); res.json({ ok: 1 });
  });

  const norm = s => (s || '').toString().trim().replace(/\s+/g, '').toLowerCase();
  function gradeOne(q, ans) {
    if (q.type === 'essay') return null;
    if (q.type === 'multiple') {
      const a = (q.answer || '').split('').sort().join('');
      const raw = Array.isArray(ans) ? ans : (ans ? ans.toString().split('') : []);
      const b = raw.sort().join('');
      return a === b;
    }
    if (q.type === 'fill') {
      const accepts = q.answer.split('|').map(norm);
      return accepts.includes(norm(ans));
    }
    return norm(q.answer) === norm(ans);
  }
  r.post('/quizzes/:id/attempt', (req, res) => {
    const q = db.prepare('SELECT * FROM quizzes WHERE id=?').get(req.params.id);
    if (!q) return res.status(404).end();
    const ids = parse(q.question_ids, []);
    const answers = req.body.answers || {};
    let score = 0, total = 0; const detail = [];
    ids.forEach(id => {
      const x = db.prepare('SELECT * FROM questions WHERE id=?').get(id);
      if (!x) return;
      total += x.score;
      const ok = gradeOne(x, answers[id]);
      if (ok) score += x.score;
      detail.push({ id: x.id, correct: ok, score: ok ? x.score : 0, max: x.score,
        pending: x.type === 'essay' });
    });
    const info = db.prepare(`INSERT INTO quiz_attempts(quiz_id,student_id,answers,score,total,started_at,submitted_at)
      VALUES(?,?,?,?,?,?,datetime('now','localtime'))`)
      .run(q.id, req.user.id, JSON.stringify(answers), score, total, req.body.started_at);
    res.json({ attemptId: info.lastInsertRowid, score: Math.round(score * 10) / 10, total, detail });
  });
  r.get('/attempts', (req, res) => {
    let rows;
    if (isT(req)) rows = db.prepare(`SELECT a.*,u.name student_name,q.title quiz_title FROM quiz_attempts a
      JOIN users u ON u.id=a.student_id JOIN quizzes q ON q.id=a.quiz_id ORDER BY a.id DESC`).all();
    else rows = db.prepare(`SELECT a.*,q.title quiz_title FROM quiz_attempts a JOIN quizzes q ON q.id=a.quiz_id
      WHERE a.student_id=? ORDER BY a.id DESC`).all(req.user.id);
    res.json(rows);
  });

  // ---------- 课题包 ----------
  r.get('/projects', (req, res) => {
    const rows = db.prepare('SELECT * FROM projects ORDER BY sort,id').all()
      .filter(p => isT(req) || p.published);
    res.json(rows.map(p => ({ ...p, tasks: parse(p.tasks, []), attachments: parse(p.attachments, []) })));
  });
  r.get('/projects/:id', (req, res) => {
    const p = db.prepare('SELECT * FROM projects WHERE id=?').get(req.params.id);
    if (!p || (!p.published && !isT(req))) return res.status(404).json({ error: '未找到' });
    let mine = null;
    if (!isT(req)) mine = db.prepare('SELECT * FROM project_submissions WHERE project_id=? AND student_id=?')
      .get(p.id, req.user.id);
    if (mine) mine = { ...mine, files: parse(mine.files, []) };
    res.json({ ...p, tasks: parse(p.tasks, []), attachments: parse(p.attachments, []), mine });
  });
  r.post('/projects', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    const b = req.body;
    const info = db.prepare(`INSERT INTO projects(title,subtitle,cover,color,category,guide,tasks,attachments,published,sort)
      VALUES(?,?,?,?,?,?,?,?,?,?)`).run(b.title, b.subtitle, b.cover || '📁', b.color || '#10B981',
      b.category, b.guide, JSON.stringify(b.tasks || []), JSON.stringify(b.attachments || []),
      b.published ? 1 : 0, b.sort || 0);
    res.json({ id: info.lastInsertRowid });
  });
  r.put('/projects/:id', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    const b = req.body;
    db.prepare(`UPDATE projects SET title=?,subtitle=?,cover=?,color=?,category=?,guide=?,tasks=?,attachments=?,published=? WHERE id=?`)
      .run(b.title, b.subtitle, b.cover, b.color, b.category, b.guide,
        JSON.stringify(b.tasks || []), JSON.stringify(b.attachments || []), b.published ? 1 : 0, req.params.id);
    res.json({ ok: 1 });
  });
  r.delete('/projects/:id', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    db.prepare('DELETE FROM projects WHERE id=?').run(req.params.id); res.json({ ok: 1 });
  });
  r.post('/projects/:id/submit', (req, res) => {
    const b = req.body;
    const exist = db.prepare('SELECT id FROM project_submissions WHERE project_id=? AND student_id=?')
      .get(req.params.id, req.user.id);
    if (exist) {
      db.prepare('UPDATE project_submissions SET content=?,files=?,submitted_at=datetime(\'now\',\'localtime\'),grade=NULL,feedback=NULL WHERE id=?')
        .run(b.content, JSON.stringify(b.files || []), exist.id);
      return res.json({ id: exist.id });
    }
    const info = db.prepare(`INSERT INTO project_submissions(project_id,student_id,content,files,submitted_at)
      VALUES(?,?,?,?,datetime('now','localtime'))`)
      .run(req.params.id, req.user.id, b.content, JSON.stringify(b.files || []));
    res.json({ id: info.lastInsertRowid });
  });
  r.get('/submissions', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    const { project_id } = req.query;
    let rows;
    if (project_id) rows = db.prepare(`SELECT s.*,u.name student_name FROM project_submissions s
      JOIN users u ON u.id=s.student_id WHERE s.project_id=? ORDER BY s.id DESC`).all(project_id);
    else rows = db.prepare(`SELECT s.*,u.name student_name,p.title project_title FROM project_submissions s
      JOIN users u ON u.id=s.student_id JOIN projects p ON p.id=s.project_id ORDER BY s.id DESC`).all();
    res.json(rows.map(s => ({ ...s, files: parse(s.files, []) })));
  });
  r.get('/my-submissions', (req, res) => {
    const rows = db.prepare(`SELECT s.*,p.title project_title FROM project_submissions s
      JOIN projects p ON p.id=s.project_id WHERE s.student_id=? ORDER BY s.id DESC`).all(req.user.id);
    res.json(rows.map(s => ({ ...s, files: parse(s.files, []) })));
  });
  r.post('/grade-submission/:id', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    const { grade, feedback } = req.body;
    const g = Number(grade);
    if (Number.isNaN(g) || g < 0 || g > 100) return res.status(400).json({ error: '成绩须为 0-100 的数值' });
    db.prepare(`UPDATE project_submissions SET grade=?,feedback=?,graded_at=datetime('now','localtime') WHERE id=?`)
      .run(g, feedback, req.params.id);
    const sub = db.prepare('SELECT * FROM project_submissions WHERE id=?').get(req.params.id);
    db.prepare('INSERT INTO notifications(student_id,title,content,link) VALUES(?,?,?,?)')
      .run(sub.student_id, '课题成绩已发布', `你的课题获得 ${g} 分，点击查看反馈。`, '/resources/projects');
    res.json({ ok: 1 });
  });

  return r;
})();
