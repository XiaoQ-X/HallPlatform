module.exports = (() => {
  const r = require('express').Router();
  const db = require('../db');
  const {fail,text,id,parse,audit,transaction}=require('../common');
  const calculations=require('../../shared/calculations.cjs');

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
    const decisions=db.prepare('SELECT * FROM cleaning_records WHERE session_id=? AND student_id=? ORDER BY julianday(updated_at),id').all(s.id,req.user.id);
    const annotated=groups.map(g=>({...g,decision:decisions.filter(d=>d.measurement_id===g.id).at(-1)?.decision||'keep'}));
    const detected=[];
    const slots=db.prepare('SELECT group_id,COUNT(DISTINCT slot) n FROM sim_measurements WHERE session_id=? GROUP BY group_id').all(s.id);
    for(const g of slots)if(g.n!==4)detected.push({code:'INCOMPLETE_GROUP',detail:g.group_id+' 缺少方向读数',occurred_at:''});
    for(const g of annotated)if(![g.IS_mA,g.IM_A,g.B_T,g.VH_mV].every(Number.isFinite))detected.push({code:'INVALID_NUMBER',detail:g.group_id+' 包含无效数值',occurred_at:''});
    const seen=new Set();for(const g of annotated){if(seen.has(g.group_id))detected.push({code:'DUPLICATE_GROUP',detail:g.group_id+' 存在重复完成记录',occurred_at:''});seen.add(g.group_id);}
    const repeatGroups=new Map();
    for(const g of annotated){const key=[g.series_id,Math.abs(g.IS_mA).toFixed(3),Math.abs(g.IM_A).toFixed(4)].join(':');if(!repeatGroups.has(key))repeatGroups.set(key,[]);repeatGroups.get(key).push(g);}
    const median=values=>{const a=[...values].sort((a,b)=>a-b);return(a[Math.floor((a.length-1)/2)]+a[Math.floor(a.length/2)])/2;};
    for(const rows of repeatGroups.values())if(rows.length>=5){const values=rows.map(g=>g.normVH_mV??g.VH_mV);if(!values.every(Number.isFinite))continue;const center=median(values),mad=median(values.map(v=>Math.abs(v-center)));rows.forEach((g,i)=>{if(mad>0?Math.abs(values[i]-center)>3*1.4826*mad:Math.abs(values[i]-center)>1e-9)detected.push({code:'REPEAT_OUTLIER',measurement_id:g.id,detail:g.group_id+' 同条件重复测量偏离中位数，请结合仪器分辨率和成因复核；不自动排除',occurred_at:''});});}
    res.json({groups:annotated,abnormals:[...abnormals,...detected],version:decisions.map(d=>d.id+':'+d.updated_at+':'+d.decision).join('|')});
  });
  r.get('/cleaning', (req, res) => {
    res.json(db.prepare('SELECT * FROM cleaning_records WHERE student_id=? ORDER BY id DESC').all(req.user.id));
  });
  r.post('/cleaning', (req, res) => {
    const b = req.body;
    if(!db.prepare('SELECT id FROM sim_sessions WHERE id=? AND student_id=?').get(id(b.session_id),req.user.id))fail('无权处理此会话',403);
    text(b.cause,'成因');text(b.action,'处理措施');
    if(b.measurement_id&&!db.prepare('SELECT id FROM sim_measurements WHERE id=? AND session_id=?').get(id(b.measurement_id),b.session_id))fail('测量点不属于该会话');
    if(!['keep','exclude'].includes(b.decision||'keep'))fail('清洗决策无效');
    const info = db.prepare(`INSERT INTO cleaning_records(student_id,session_id,label,point_label,abnormal_code,cause,action,resolved)
      VALUES(?,?,?,?,?,?,?,?)`).run(req.user.id, b.session_id, b.label, b.point_label,
      b.abnormal_code, b.cause, b.action, b.resolved ? 1 : 0);
    db.prepare("UPDATE cleaning_records SET measurement_id=?,decision=?,updated_at=strftime('%Y-%m-%d %H:%M:%f','now') WHERE id=?").run(b.measurement_id||null,b.decision||'keep',info.lastInsertRowid);
    res.json({ id: info.lastInsertRowid });
  });
  r.put('/cleaning/:id', (req, res) => {
    const b = req.body;
    const old=db.prepare('SELECT * FROM cleaning_records WHERE id=? AND student_id=?').get(id(req.params.id),req.user.id);if(!old)fail('记录不存在',404);text(b.cause,'成因');text(b.action,'处理措施');if(!['keep','exclude'].includes(b.decision||old.decision))fail('清洗决策无效');
    db.prepare('UPDATE cleaning_records SET cause=?,action=?,resolved=? WHERE id=? AND student_id=?')
      .run(b.cause, b.action, b.resolved ? 1 : 0, req.params.id, req.user.id);
    db.prepare("UPDATE cleaning_records SET decision=?,updated_at=strftime('%Y-%m-%d %H:%M:%f','now') WHERE id=?").run(b.decision||old.decision,old.id);audit(req.user.id,'cleaning-edit',old.id,old,b);
    res.json({ ok: 1 });
  });
  r.delete('/cleaning/:id', (req, res) => {
    const old=db.prepare('SELECT * FROM cleaning_records WHERE id=? AND student_id=?').get(id(req.params.id),req.user.id);if(!old)fail('记录不存在',404);audit(req.user.id,'cleaning-delete',old.id,old,null);
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
    text(b.title,'标题',200);let result;try{result=calculations.uncertainty(b.inputs||{});}catch(e){fail(e.message);}
    const info = db.prepare('INSERT INTO uncertainty_records(student_id,title,inputs,result) VALUES(?,?,?,?)')
      .run(req.user.id, b.title, JSON.stringify(b.inputs), JSON.stringify(result));
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
    text(b.title,'标题',200);let result;try{result=calculations.fit(b.dataset,b.model);}catch(e){fail(e.message);}
    if(b.provenance?.session_id&&!db.prepare('SELECT id FROM sim_sessions WHERE id=? AND student_id=?').get(id(b.provenance.session_id),req.user.id))fail('数据来源无权访问',403);
    const provenance={...b.provenance,algorithm:result.algorithm};
    if(provenance.session_id){
      const source=db.prepare('SELECT * FROM sim_measurements WHERE session_id=? AND group_complete=1 ORDER BY id').all(provenance.session_id);
      const decisions=db.prepare('SELECT * FROM cleaning_records WHERE session_id=? AND student_id=? ORDER BY julianday(updated_at),id').all(provenance.session_id,req.user.id);
      const version=decisions.map(d=>d.id+':'+d.updated_at+':'+d.decision).join('|');
      if(!provenance.manual){
        if(provenance.version!==version)fail('清洗决策已更新，请重新导入',409);
        if(!['IS_mA','IM_A','B_T'].includes(provenance.xfield)||(b.model==='multi'&&!['IS_mA','IM_A','B_T'].includes(provenance.zfield)))fail('数据轴无效');
        for(const point of b.dataset){const row=source.find(g=>g.id===point.measurement_id);if(!row||point.x!==row[provenance.xfield]||point.y!==(row.normVH_mV??row.VH_mV)||(b.model==='multi'&&point.z!==row[provenance.zfield]))fail('数据与原始记录不一致，请标记为手工修订');if(provenance.cleaned&&decisions.filter(d=>d.measurement_id===row.id).at(-1)?.decision==='exclude')fail('清洗排除点不可作为有效原始点');}
      }
      provenance.sourceSnapshot=source.filter(g=>b.dataset.some(p=>p.measurement_id===g.id));provenance.cleaningSnapshot=decisions;
    }
    provenance.datasetSha256=require('crypto').createHash('sha256').update(JSON.stringify(b.dataset)).digest('hex');
    const info = db.prepare('INSERT INTO analysis_records(student_id,title,model,dataset,params,metrics) VALUES(?,?,?,?,?,?)')
      .run(req.user.id, b.title, b.model, JSON.stringify(b.dataset),
        JSON.stringify({coefs:result.coefs,a:result.a,b:result.b}), JSON.stringify({r2:result.r2,rmse:result.rmse,dof:result.dof,standardErrors:result.standardErrors,ci95:result.ci95}));
    db.prepare('UPDATE analysis_records SET provenance=? WHERE id=?').run(JSON.stringify(provenance),info.lastInsertRowid);
    res.json({ id: info.lastInsertRowid });
  });
  r.delete('/analysis/:id', (req, res) => {
    db.prepare('DELETE FROM analysis_records WHERE id=? AND student_id=?').run(req.params.id, req.user.id);
    res.json({ ok: 1 });
  });

  const safe = s => { try { return JSON.parse(s); } catch { return {}; } };
  return r;
})();
