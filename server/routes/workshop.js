module.exports = (() => {
  const r = require('express').Router();
  const db = require('../db');

  // ---------- 数据清洗 ----------
  r.get('/sessions-brief', (req, res) => {
    const rows = db.prepare(`SELECT s.id,s.started_at,s.finished,
      COUNT(DISTINCT m.group_id) groups,
      (SELECT COUNT(*) FROM sim_abnormals a WHERE a.session_id=s.id) errors
      FROM sim_sessions s LEFT JOIN sim_measurements m
      ON m.session_id=s.id AND m.group_complete=1
      WHERE s.student_id=? GROUP BY s.id ORDER BY s.id DESC`).all(req.user.id);
    res.json(rows);
  });
  r.get('/session-points/:id', (req, res) => {
    const s = db.prepare('SELECT * FROM sim_sessions WHERE id=? AND student_id=?').get(req.params.id, req.user.id);
    if (!s) return res.status(404).end();
    const groups = db.prepare(`SELECT * FROM sim_measurements WHERE session_id=? AND group_complete=1 ORDER BY id`).all(s.id);
    const abnormals = db.prepare('SELECT * FROM sim_abnormals WHERE session_id=? ORDER BY id').all(s.id);
    res.json({ groups, abnormals });
  });
  r.get('/cleaning', (req, res) => {
    res.json(db.prepare('SELECT * FROM cleaning_records WHERE student_id=? ORDER BY id DESC').all(req.user.id));
  });
  r.post('/cleaning', (req, res) => {
    const b = req.body;
    const info = db.prepare(`INSERT INTO cleaning_records(student_id,session_id,label,point_label,abnormal_code,cause,action,resolved)
      VALUES(?,?,?,?,?,?,?,?)`).run(req.user.id, b.session_id, b.label, b.point_label,
      b.abnormal_code, b.cause, b.action, b.resolved ? 1 : 0);
    res.json({ id: info.lastInsertRowid });
  });
  r.put('/cleaning/:id', (req, res) => {
    const b = req.body;
    db.prepare('UPDATE cleaning_records SET cause=?,action=?,resolved=? WHERE id=? AND student_id=?')
      .run(b.cause, b.action, b.resolved ? 1 : 0, req.params.id, req.user.id);
    res.json({ ok: 1 });
  });
  r.delete('/cleaning/:id', (req, res) => {
    db.prepare('DELETE FROM cleaning_records WHERE id=? AND student_id=?').run(req.params.id, req.user.id);
    res.json({ ok: 1 });
  });

  // ---------- 不确定度 ----------
  r.get('/uncertainty', (req, res) => {
    res.json(db.prepare('SELECT * FROM uncertainty_records WHERE student_id=? ORDER BY id DESC').all(req.user.id)
      .map(x => ({ ...x, inputs: safe(x.inputs), result: safe(x.result) })));
  });
  r.post('/uncertainty', (req, res) => {
    const b = req.body;
    const info = db.prepare('INSERT INTO uncertainty_records(student_id,title,inputs,result) VALUES(?,?,?,?)')
      .run(req.user.id, b.title, JSON.stringify(b.inputs), JSON.stringify(b.result));
    res.json({ id: info.lastInsertRowid });
  });
  r.delete('/uncertainty/:id', (req, res) => {
    db.prepare('DELETE FROM uncertainty_records WHERE id=? AND student_id=?').run(req.params.id, req.user.id);
    res.json({ ok: 1 });
  });

  // ---------- 多元分析 ----------
  r.get('/analysis', (req, res) => {
    res.json(db.prepare('SELECT * FROM analysis_records WHERE student_id=? ORDER BY id DESC').all(req.user.id)
      .map(x => ({ ...x, dataset: safe(x.dataset), params: safe(x.params), metrics: safe(x.metrics) })));
  });
  r.post('/analysis', (req, res) => {
    const b = req.body;
    const info = db.prepare('INSERT INTO analysis_records(student_id,title,model,dataset,params,metrics) VALUES(?,?,?,?,?,?)')
      .run(req.user.id, b.title, b.model, JSON.stringify(b.dataset),
        JSON.stringify(b.params), JSON.stringify(b.metrics));
    res.json({ id: info.lastInsertRowid });
  });
  r.delete('/analysis/:id', (req, res) => {
    db.prepare('DELETE FROM analysis_records WHERE id=? AND student_id=?').run(req.params.id, req.user.id);
    res.json({ ok: 1 });
  });

  const safe = s => { try { return JSON.parse(s); } catch { return {}; } };
  return r;
})();
