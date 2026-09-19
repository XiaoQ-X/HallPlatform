module.exports = (() => {
  const r = require('express').Router();
  const db = require('../db');

  r.post('/start', (req, res) => {
    const { case_id } = req.body;
    const info = db.prepare('INSERT INTO sim_sessions(student_id,case_id,unity_session,state) VALUES(?,?,?,?)')
      .run(req.user.id, case_id || 'hall-basic', null, '{}');
    res.json({ platformSessionId: info.lastInsertRowid });
  });

  r.post('/events', (req, res) => {
    const events = Array.isArray(req.body.events) ? req.body.events : [req.body.event || req.body];
    let psid = req.body.platformSessionId;
    const saved = [];
    events.forEach(ev => {
      if (!ev || ev.isDemo) return;
      if (!psid) {
        const open = db.prepare('SELECT id FROM sim_sessions WHERE student_id=? AND finished=0 ORDER BY id DESC LIMIT 1')
          .get(req.user.id);
        psid = open?.id;
        if (!psid) {
          const info = db.prepare('INSERT INTO sim_sessions(student_id,case_id,unity_session,state) VALUES(?,?,?,?)')
            .run(req.user.id, ev.caseId || 'hall-basic', ev.sessionId, JSON.stringify(ev.state || {}));
          psid = info.lastInsertRowid;
        }
      }
      // 绑定 unity session
      if (ev.sessionId) db.prepare('UPDATE sim_sessions SET unity_session=? WHERE id=?').run(ev.sessionId, psid);
      try {
        const info = db.prepare('INSERT OR IGNORE INTO sim_events(session_id,type,event_id,payload) VALUES(?,?,?,?)')
          .run(psid, ev.type, ev.eventId, JSON.stringify(ev));
        if (info.changes) saved.push(ev.eventId);
      } catch {}

      if (ev.type === 'OnMeasureData' && ev.measurement) {
        const m = ev.measurement;
        db.prepare(`INSERT INTO sim_measurements(session_id,group_id,slot,is_dir,im_dir,v_dir,IS_mA,IM_A,B_T,raw_mV,VH_mV,normVH_mV,group_complete,payload,measured_at)
          VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)`).run(psid, m.groupId, m.slot, m.isDirection, m.imDirection,
          m.voltageDirection, m.IS_mA, m.IM_A, m.B_T, m.rawVoltage_mV, m.VH_mV, m.normalizedVH_mV,
          m.groupComplete ? 1 : 0, JSON.stringify(m), ev.timestamp);
      }
      if (ev.type === 'OnAbnormalEvent') {
        db.prepare('INSERT INTO sim_abnormals(session_id,code,detail,success,step,payload) VALUES(?,?,?,?,?,?)')
          .run(psid, ev.code, ev.detail || '', ev.success ? 1 : 0, ev.step, JSON.stringify(ev));
      }
      if (ev.type === 'OnExperimentComplete') {
        db.prepare(`UPDATE sim_sessions SET finished=1,finished_at=datetime('now','localtime'),state=? WHERE id=?`)
          .run(JSON.stringify(ev.state || {}), psid);
      }
      if (ev.state) db.prepare('UPDATE sim_sessions SET state=? WHERE id=?').run(JSON.stringify(ev.state), psid);
    });
    res.json({ ok: 1, saved: saved.length });
  });

  r.get('/sessions', (req, res) => {
    const rows = db.prepare(`SELECT s.*, COUNT(m.id) points FROM sim_sessions s
      LEFT JOIN sim_measurements m ON m.session_id=s.id AND m.group_complete=1
      WHERE s.student_id=? GROUP BY s.id ORDER BY s.id DESC`).all(req.user.id);
    res.json(rows);
  });
  r.get('/sessions/:id', (req, res) => {
    const s = db.prepare('SELECT * FROM sim_sessions WHERE id=?').get(req.params.id);
    if (!s) return res.status(404).end();
    if (req.user.role !== 'teacher' && s.student_id !== req.user.id) return res.status(403).end();
    const measurements = db.prepare('SELECT * FROM sim_measurements WHERE session_id=? ORDER BY id').all(s.id);
    const abnormals = db.prepare('SELECT * FROM sim_abnormals WHERE session_id=? ORDER BY id').all(s.id);
    const events = db.prepare('SELECT type,event_id,received_at FROM sim_events WHERE session_id=? ORDER BY id').all(s.id);
    res.json({ ...s, state: safe(s.state), measurements, abnormals, events });
  });
  const safe = s => { try { return JSON.parse(s); } catch { return {}; } };

  return r;
})();
