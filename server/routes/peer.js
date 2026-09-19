module.exports = (() => {
  const r = require('express').Router();
  const db = require('../db');
  const isT = req => req.user.role === 'teacher';
  const safe = s => { try { return JSON.parse(s); } catch { return s; } };

  r.get('/rubrics', (req, res) => {
    res.json(db.prepare('SELECT * FROM rubrics ORDER BY id').all()
      .map(x => ({ ...x, dimensions: safe(x.dimensions) })));
  });

  // 作品
  r.post('/items', (req, res) => {
    const b = req.body;
    const info = db.prepare(`INSERT INTO review_items(student_id,item_type,title,summary,recording,files,session_id)
      VALUES(?,?,?,?,?,?,?)`).run(req.user.id, b.item_type || 'experiment', b.title || null, b.summary || null,
      b.recording || null, JSON.stringify(b.files || []), b.session_id || null);
    res.json({ id: info.lastInsertRowid });
  });
  r.get('/items', (req, res) => {
    const me = db.prepare('SELECT * FROM users WHERE id=?').get(req.user.id);
    let rows;
    if (isT(req)) rows = db.prepare(`SELECT i.*,u.name student_name FROM review_items i
      JOIN users u ON u.id=i.student_id ORDER BY i.id DESC`).all();
    else rows = db.prepare(`SELECT i.*,u.name student_name FROM review_items i
      JOIN users u ON u.id=i.student_id
      WHERE u.class_id=? AND i.student_id<>? ORDER BY i.id DESC`)
      .all(me.class_id, req.user.id);
    res.json(rows.map(x => ({ ...x, files: safe(x.files),
      reviewed: !!db.prepare('SELECT id FROM reviews WHERE item_id=? AND reviewer_id=?')
        .get(x.id, req.user.id) })));
  });
  r.get('/my-items', (req, res) => {
    const rows = db.prepare('SELECT * FROM review_items WHERE student_id=? ORDER BY id DESC').all(req.user.id);
    res.json(rows.map(x => ({
      ...x, files: safe(x.files),
      reviews: db.prepare(`SELECT rv.*,u.name reviewer_name FROM reviews rv
        JOIN users u ON u.id=rv.reviewer_id WHERE rv.item_id=?`).all(x.id)
        .map(rv => ({ ...rv, scores: safe(rv.scores) }))
    })));
  });
  r.get('/items/:id', (req, res) => {
    const x = db.prepare(`SELECT i.*,u.name student_name,u.class_id FROM review_items i
      JOIN users u ON u.id=i.student_id WHERE i.id=?`).get(req.params.id);
    if (!x) return res.status(404).end();
    if (!isT(req)) {
      const me = db.prepare('SELECT class_id FROM users WHERE id=?').get(req.user.id);
      if (x.student_id !== req.user.id && x.class_id !== me.class_id)
        return res.status(403).json({ error: '只能查看同班作品' });
    }
    const reviews = db.prepare(`SELECT rv.*,u.name reviewer_name FROM reviews rv
      JOIN users u ON u.id=rv.reviewer_id WHERE rv.item_id=?`).all(x.id)
      .map(rv => ({ ...rv, scores: safe(rv.scores), mine: rv.reviewer_id === req.user.id }));
    res.json({ ...x, files: safe(x.files), reviews });
  });
  r.post('/items/:id/review', (req, res) => {
    const b = req.body;
    const item = db.prepare(`SELECT i.*,u.class_id FROM review_items i
      JOIN users u ON u.id=i.student_id WHERE i.id=?`).get(req.params.id);
    if (!item) return res.status(404).end();
    if (!isT(req)) {
      const me = db.prepare('SELECT class_id FROM users WHERE id=?').get(req.user.id);
      if (item.student_id === req.user.id)
        return res.status(400).json({ error: '不能评价自己的作品' });
      if (item.class_id !== me.class_id)
        return res.status(403).json({ error: '只能评价同班作品' });
    }
    const exist = db.prepare('SELECT id FROM reviews WHERE item_id=? AND reviewer_id=?')
      .get(req.params.id, req.user.id);
    if (exist) {
      db.prepare('UPDATE reviews SET scores=?,comment=?,rubric_id=? WHERE id=?')
        .run(JSON.stringify(b.scores || {}), b.comment, b.rubric_id, exist.id);
      return res.json({ id: exist.id });
    }
    const info = db.prepare('INSERT INTO reviews(item_id,reviewer_id,rubric_id,scores,comment) VALUES(?,?,?,?,?)')
      .run(req.params.id, req.user.id, b.rubric_id, JSON.stringify(b.scores || {}), b.comment);
    db.prepare('INSERT INTO notifications(student_id,title,content,link) VALUES(?,?,?,?)')
      .run(item.student_id, '收到新的同伴评价', `《${item.title}》收到了新的评价。`, '/peer/works');
    res.json({ id: info.lastInsertRowid });
  });
  r.get('/my-reviews', (req, res) => {
    res.json(db.prepare(`SELECT rv.*,i.title item_title,u.name owner_name FROM reviews rv
      JOIN review_items i ON i.id=rv.item_id JOIN users u ON u.id=i.student_id
      WHERE rv.reviewer_id=? ORDER BY rv.id DESC`).all(req.user.id));
  });

  // 申诉
  r.post('/appeals', (req, res) => {
    const b = req.body;
    if (!b.content || !String(b.content).trim())
      return res.status(400).json({ error: '请填写申诉理由' });
    if (b.ref_type === 'review') {
      const row = db.prepare(`SELECT i.student_id FROM reviews rv
        JOIN review_items i ON i.id=rv.item_id WHERE rv.id=?`).get(b.ref_id);
      if (!row) return res.status(400).json({ error: '申诉的评价不存在' });
      if (row.student_id !== req.user.id)
        return res.status(403).json({ error: '只能对你本人收到的评价发起申诉' });
    }
    const dup = db.prepare('SELECT id FROM appeals WHERE ref_type=? AND ref_id=? AND student_id=? AND status<>?')
      .get(b.ref_type, b.ref_id, req.user.id, '已回复');
    if (dup) return res.status(400).json({ error: '该申诉正在处理中，请勿重复提交' });
    const info = db.prepare('INSERT INTO appeals(ref_type,ref_id,student_id,content) VALUES(?,?,?,?)')
      .run(b.ref_type, b.ref_id, req.user.id, String(b.content).trim());
    res.json({ id: info.lastInsertRowid });
  });
  r.get('/appeals', (req, res) => {
    let rows;
    if (isT(req)) rows = db.prepare(`SELECT a.*,u.name student_name FROM appeals a
      JOIN users u ON u.id=a.student_id ORDER BY a.id DESC`).all();
    else rows = db.prepare('SELECT * FROM appeals WHERE student_id=? ORDER BY id DESC').all(req.user.id);
    res.json(rows);
  });
  r.post('/appeals/:id/reply', (req, res) => {
    if (!isT(req)) return res.status(403).end();
    db.prepare(`UPDATE appeals SET reply=?,status='已回复',replied_at=datetime('now','localtime') WHERE id=?`)
      .run(req.body.reply, req.params.id);
    const a = db.prepare('SELECT * FROM appeals WHERE id=?').get(req.params.id);
    db.prepare('INSERT INTO notifications(student_id,title,content,link) VALUES(?,?,?,?)')
      .run(a.student_id, '申诉已回复', '你的申诉教师已回复，请查看。', '/peer/appeals');
    res.json({ ok: 1 });
  });

  return r;
})();
