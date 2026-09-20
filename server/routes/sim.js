const r=require('express').Router();const {db,fail,parse,text,number,id,transaction,studentAccess}=require('../common');
function own(req,key){const s=db.prepare('SELECT * FROM sim_sessions WHERE id=?').get(id(key));if(!s)fail('会话不存在',404);if(s.student_id!==req.user.id)fail('只能写入本人会话',403);return s;}
r.post('/start',(req,res)=>{
  const caseId=req.body.case_id||'hall-basic';text(caseId,'案例编号',100);
  let cfg={material:'n-silicon',thickness_mm:.5,maxIs_mA:10,maxIm_A:1};
  if(caseId!=='hall-basic'){const owner=req.user.role==='teacher'?req.user.id:db.prepare('SELECT teacher_id FROM classes WHERE id=?').get(req.user.class_id)?.teacher_id;const c=db.prepare('SELECT sim_config FROM cases WHERE id=? AND published=1 AND archived=0 AND owner_id=?').get(id(caseId.replace(/^case-/,'')),owner||0);if(!c)fail('案例不可用');cfg={...cfg,...parse(c.sim_config)};}
  if(cfg.material!=='n-silicon'||cfg.thickness_mm!==.5||cfg.maxIs_mA>10||cfg.maxIm_A>1)fail('案例配置不受引擎支持');
  const result=db.prepare('INSERT INTO sim_sessions(student_id,case_id,config,state) VALUES(?,?,?,?)').run(req.user.id,caseId,JSON.stringify(cfg),'{}');res.json({platformSessionId:result.lastInsertRowid,config:cfg});
});
r.post('/events',(req,res)=>{
  const session=own(req,req.body.platformSessionId);const events=req.body.events||[req.body.event||req.body];if(!Array.isArray(events)||events.length>300)fail('事件批次无效');
  const ack=transaction(()=>{
    const ack=[];let s=session;
    for(const ev of events){
      if(!ev||typeof ev!=='object')fail('事件无效');text(ev.eventId,'事件ID',200);text(ev.type,'事件类型',80);
      const old=db.prepare('SELECT session_id,payload FROM sim_events WHERE event_id=?').get(ev.eventId);
      if(old){if(old.session_id!==s.id||old.payload!==JSON.stringify(ev))fail('事件ID冲突',409);ack.push(ev.eventId);continue;}
      if(ev.isDemo){ack.push(ev.eventId);continue;}
      if(s.status==='abandoned')fail('会话已放弃',409);
      if(s.finished&&['OnMeasureData','OnAbnormalEvent','OnExperimentComplete'].includes(ev.type))fail('实验已结束',409);
      if(ev.sessionId){text(ev.sessionId,'Unity会话',100);if(s.unity_session&&s.unity_session!==ev.sessionId)fail('Unity会话不匹配',409);if(!s.unity_session){db.prepare('UPDATE sim_sessions SET unity_session=? WHERE id=?').run(ev.sessionId,s.id);s={...s,unity_session:ev.sessionId};}}
      db.prepare('INSERT INTO sim_events(session_id,type,event_id,payload) VALUES(?,?,?,?)').run(s.id,ev.type,ev.eventId,JSON.stringify(ev));
      if(ev.type==='OnMeasureData'){
        const m=ev.measurement;if(!m)fail('缺少测量数据');text(m.groupId,'组编号',200);number(m.slot,'方向槽位',1,4);if(!Number.isInteger(m.slot))fail('方向槽位无效');
        for(const k of ['isDirection','imDirection','voltageDirection'])if(![-1,1].includes(m[k]))fail('方向无效');
        number(m.IS_mA,'工作电流',-10,10);number(m.IM_A,'励磁电流',-1,1);number(m.B_T,'磁场',-100,100);number(m.rawVoltage_mV,'原始电压',-200,200);
        let vh=null;
        if(m.groupComplete){const prior=db.prepare('SELECT slot,raw_mV FROM sim_measurements WHERE session_id=? AND group_id=? ORDER BY id').all(s.id,m.groupId);const raw=new Map(prior.map(x=>[x.slot,x.raw_mV]));raw.set(m.slot,m.rawVoltage_mV);if(raw.size!==4)fail('缺少完整四方向原始读数');vh=(raw.get(1)-raw.get(2)+raw.get(3)-raw.get(4))/4;}
        db.prepare('INSERT INTO sim_measurements(session_id,group_id,slot,is_dir,im_dir,v_dir,IS_mA,IM_A,B_T,raw_mV,VH_mV,normVH_mV,group_complete,payload,measured_at,event_id,series_id) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)').run(s.id,m.groupId,m.slot,m.isDirection,m.imDirection,m.voltageDirection,m.IS_mA,m.IM_A,m.B_T,m.rawVoltage_mV,vh,vh==null?null:vh*m.voltageDirection,m.groupComplete?1:0,JSON.stringify(m),ev.timestamp||new Date().toISOString(),ev.eventId,m.row?.seriesId||'legacy');
      }
      if(ev.type==='OnAbnormalEvent')db.prepare('INSERT INTO sim_abnormals(session_id,code,detail,success,step,payload,event_id) VALUES(?,?,?,?,?,?,?)').run(s.id,text(ev.code,'异常代码',100),String(ev.detail||'').slice(0,2000),ev.success?1:0,Number.isInteger(ev.step)?ev.step:null,JSON.stringify(ev),ev.eventId);
      if(ev.type==='OnExperimentComplete'){
        if(!db.prepare('SELECT id FROM sim_measurements WHERE session_id=? AND group_complete=1').get(s.id))fail('至少完成一组四方向测量才能结束');
        db.prepare("UPDATE sim_sessions SET finished=1,status='finished',finished_at=CURRENT_TIMESTAMP WHERE id=?").run(s.id);s={...s,finished:1};
      }
      if(ev.state&&typeof ev.state==='object')db.prepare('UPDATE sim_sessions SET state=? WHERE id=?').run(JSON.stringify(ev.state),s.id);ack.push(ev.eventId);
    }return ack;
  });res.json({ok:1,saved:ack.length,ack});
});
r.post('/sessions/:id/abandon',(req,res)=>{const s=own(req,req.params.id);if(s.finished)fail('已完成会话不能放弃');db.prepare("UPDATE sim_sessions SET status='abandoned' WHERE id=?").run(s.id);res.json({ok:1});});
r.get('/sessions',(req,res)=>res.json(db.prepare('SELECT s.*,COUNT(DISTINCT CASE WHEN m.group_complete=1 THEN m.group_id END) points FROM sim_sessions s LEFT JOIN sim_measurements m ON m.session_id=s.id WHERE s.student_id=? GROUP BY s.id ORDER BY s.id DESC').all(req.user.id)));
r.get('/sessions/:id',(req,res)=>{const s=db.prepare('SELECT * FROM sim_sessions WHERE id=?').get(id(req.params.id));if(!s)fail('会话不存在',404);studentAccess(req.user,s.student_id);res.json({...s,state:parse(s.state),config:parse(s.config),measurements:db.prepare('SELECT * FROM sim_measurements WHERE session_id=? ORDER BY id').all(s.id),abnormals:db.prepare('SELECT * FROM sim_abnormals WHERE session_id=? ORDER BY id').all(s.id),events:db.prepare('SELECT type,event_id,received_at FROM sim_events WHERE session_id=? ORDER BY id').all(s.id)});});module.exports=r;
