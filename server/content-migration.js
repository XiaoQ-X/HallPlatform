module.exports=db=>{
  if(db.prepare('SELECT 1 FROM migrations WHERE version=141').get())return;
  db.exec('BEGIN IMMEDIATE');try{
    const first=db.prepare("SELECT id FROM users WHERE role='teacher' ORDER BY id LIMIT 1").get()?.id||null;
    for(const table of ['cases','ideology','questions','quizzes','projects','rubrics']){db.exec(`ALTER TABLE ${table} ADD COLUMN owner_id INTEGER REFERENCES users(id)`);db.prepare(`UPDATE ${table} SET owner_id=?`).run(first);}
    const tasks=[
      ['经典霍尔响应与开关阈值的区别','固定IS，改变IM，观察连续霍尔电压。开关阶跃需要额外的阈值电路，本实验不模拟施密特触发器。'],
      ['电流传感器原理类比','固定IS，在0至1A范围扫描励磁电流IM，拟合VH与IM关系；IM在此作为被测电流产生磁场的类比量。'],
      ['磁场方向与霍尔电压','完成四方向测量，观察电压符号变化。三路传感器及电机换相时序属于资料拓展，本实验不模拟电机。'],
      ['运动电动势资料对比','本实验仅验证经典霍尔电压随磁场的关系。电磁流量计基于运动电动势E=BDv，改变磁场不等于改变流速。'],
      ['经典霍尔与量子霍尔资料对比','测量经典霍尔电压，区分霍尔电阻VH/IS与霍尔系数VH*d/(IS*B)。量子平台须查阅低温二维电子气资料，本实验不模拟量子效应。']
    ];
    for(const c of db.prepare('SELECT * FROM cases').all()){
      let cfg={};try{cfg=JSON.parse(c.sim_config)||{};}catch{}
      const task=tasks[c.id-1];if(!task)continue;
      const original=JSON.stringify(c);
      const config={...cfg,material:'n-silicon',thickness_mm:.5,maxIs_mA:10,maxIm_A:1,taskTitle:task[0],taskGoal:task[1]};
      let content=c.content;
      if(c.id===2)content=content.replace('固定励磁电流、改变工作电流','固定工作电流、改变励磁电流');
      if(c.id===5)content=content.replaceAll('R_H=\\dfrac{h}{\\nu e^2}','R_{xy}=\\dfrac{h}{\\nu e^2}');
      db.prepare('UPDATE cases SET sim_config=?,content=? WHERE id=?').run(JSON.stringify(config),content,c.id);
      db.prepare('INSERT INTO audit_log(action,target,before_json,after_json) VALUES(?,?,?,?)').run('migration-141','case:'+c.id,original,JSON.stringify({config,content}));
    }
    const q=db.prepare('SELECT * FROM questions WHERE id=11').get();
    if(q?.stem.includes('霍尔传感器常见应用'))db.prepare('UPDATE questions SET answer=?,analysis=? WHERE id=11').run('ABC','电磁流量计通常基于运动电动势，不属于此题所述霍尔传感器应用。');
    db.prepare('UPDATE questions SET stem=REPLACE(stem,?,?),analysis=? WHERE id=8').run('R_K = h/(νe²)','R_xy = R_K/ν，R_K = h/e²','冯·克利青常数R_K=h/e²；第ν个平台的霍尔电阻为R_K/ν。');
    db.prepare('UPDATE questions SET stem=REPLACE(stem,?,?) WHERE id=20').run('本平台样品固定为','当前经典霍尔实验样品为');
    for(const p of db.prepare('SELECT * FROM projects').all()){
      const attachments=JSON.parse(p.attachments||'[]');
      if(attachments.some(a=>!a.url))db.prepare('UPDATE projects SET attachments=? WHERE id=?').run(JSON.stringify(attachments.filter(a=>a.url)),p.id);
      if(p.id===3){let tasks=JSON.parse(p.tasks||'[]');tasks=tasks.map(t=>t.t.includes('纵向电压')?{...t,t:'由教师提供电导率σ及其不确定度；或在实物设备上测量纵向电压与几何参数求σ'}:t);db.prepare('UPDATE projects SET tasks=? WHERE id=?').run(JSON.stringify(tasks),p.id);}
    }
    for(const rubric of db.prepare('SELECT * FROM rubrics').all()){
      const dims=JSON.parse(rubric.dimensions);for(const d of dims)d.desc=(d.desc||d.name)+'；优秀（80%–100%）：证据完整、步骤正确且能解释依据；达标（60%–79%）：主体正确但论证或记录不完整；待改进（1%–59%）：关键步骤或证据缺失；0：未提供相关证据。';
      db.prepare('UPDATE rubrics SET dimensions=? WHERE id=?').run(JSON.stringify(dims),rubric.id);
    }
    db.exec(`CREATE TRIGGER question_score_insert BEFORE INSERT ON questions WHEN typeof(NEW.score) NOT IN ('real','integer') OR NEW.score<=0 BEGIN SELECT RAISE(ABORT,'invalid question score'); END;
      CREATE TRIGGER question_score_update BEFORE UPDATE OF score ON questions WHEN typeof(NEW.score) NOT IN ('real','integer') OR NEW.score<=0 BEGIN SELECT RAISE(ABORT,'invalid question score'); END;
      CREATE TRIGGER submission_parent_insert BEFORE INSERT ON project_submissions WHEN NOT EXISTS(SELECT 1 FROM projects WHERE id=NEW.project_id) OR NOT EXISTS(SELECT 1 FROM users WHERE id=NEW.student_id) BEGIN SELECT RAISE(ABORT,'invalid submission reference'); END;
      CREATE TRIGGER event_parent_insert BEFORE INSERT ON sim_events WHEN NOT EXISTS(SELECT 1 FROM sim_sessions WHERE id=NEW.session_id) BEGIN SELECT RAISE(ABORT,'invalid session reference'); END;`);
    db.prepare('INSERT INTO migrations(version) VALUES(141)').run();db.exec('COMMIT');
  }catch(e){db.exec('ROLLBACK');throw e;}
};
