const fs=require('fs');const path=require('path');
module.exports=(db,dataDir)=>{
  db.exec('CREATE TABLE IF NOT EXISTS migrations(version INTEGER PRIMARY KEY,applied_at TEXT DEFAULT CURRENT_TIMESTAMP)');
  if(db.prepare('SELECT 1 FROM migrations WHERE version=140').get())return;
  const backup=path.join(dataDir,'before-v1.4.db');
  if(!fs.existsSync(backup))db.exec("VACUUM INTO '"+backup.replace(/'/g,"''")+"'");
  db.exec('BEGIN IMMEDIATE');
  try{
    const cols={
      users:{token_version:'INTEGER NOT NULL DEFAULT 1',active:'INTEGER NOT NULL DEFAULT 1',must_change:'INTEGER NOT NULL DEFAULT 0'},
      classes:{teacher_id:'INTEGER REFERENCES users(id)'},
      sim_sessions:{status:"TEXT NOT NULL DEFAULT 'active'",demo:'INTEGER NOT NULL DEFAULT 0',config:"TEXT NOT NULL DEFAULT '{}'",verified:'INTEGER NOT NULL DEFAULT 0',quality_score:'REAL'},
      sim_measurements:{event_id:'TEXT',series_id:"TEXT DEFAULT 'legacy'"},sim_abnormals:{event_id:'TEXT'},
      cleaning_records:{measurement_id:'INTEGER REFERENCES sim_measurements(id)',decision:"TEXT DEFAULT 'keep'",updated_at:'TEXT'},
      analysis_records:{provenance:"TEXT DEFAULT '{}'"},
      quiz_attempts:{snapshot:"TEXT DEFAULT '[]'",detail:"TEXT DEFAULT '[]'",status:"TEXT DEFAULT 'graded'",deadline:'TEXT'},
      project_submissions:{version:'INTEGER NOT NULL DEFAULT 1',task_progress:"TEXT DEFAULT '[]'",snapshot:"TEXT DEFAULT '{}'"},
      reviews:{snapshot:"TEXT DEFAULT '[]'",voided:'INTEGER NOT NULL DEFAULT 0',updated_at:'TEXT'},
      review_items:{rubric_id:'INTEGER REFERENCES rubrics(id)',submission_id:'INTEGER REFERENCES project_submissions(id)'},
      appeals:{snapshot:"TEXT DEFAULT '{}'",decision:'TEXT',handled_by:'INTEGER REFERENCES users(id)'},rubrics:{archived:'INTEGER NOT NULL DEFAULT 0'}
    };
    for(const [table,fields]of Object.entries(cols))for(const [name,type]of Object.entries(fields))db.exec(`ALTER TABLE ${table} ADD COLUMN ${name} ${type}`);
    for(const table of ['cases','ideology','questions','quizzes','projects'])db.exec(`ALTER TABLE ${table} ADD COLUMN archived INTEGER NOT NULL DEFAULT 0`);
    db.exec(`
      CREATE TABLE audit_log(id INTEGER PRIMARY KEY,actor_id INTEGER,action TEXT NOT NULL,target TEXT NOT NULL,before_json TEXT,after_json TEXT,created_at TEXT DEFAULT CURRENT_TIMESTAMP);
      CREATE TABLE files(id TEXT PRIMARY KEY,owner_id INTEGER NOT NULL REFERENCES users(id),name TEXT NOT NULL,path TEXT UNIQUE NOT NULL,mime TEXT NOT NULL,size INTEGER NOT NULL CHECK(size>=0),created_at TEXT DEFAULT CURRENT_TIMESTAMP);
      CREATE TABLE submission_versions(id INTEGER PRIMARY KEY,submission_id INTEGER NOT NULL REFERENCES project_submissions(id),version INTEGER NOT NULL,payload TEXT NOT NULL,created_at TEXT DEFAULT CURRENT_TIMESTAMP);
      CREATE TABLE project_progress(student_id INTEGER REFERENCES users(id),project_id INTEGER REFERENCES projects(id),progress TEXT NOT NULL,PRIMARY KEY(student_id,project_id));
      CREATE TABLE grade_publications(id INTEGER PRIMARY KEY,student_id INTEGER NOT NULL REFERENCES users(id),teacher_id INTEGER NOT NULL REFERENCES users(id),payload TEXT NOT NULL,created_at TEXT DEFAULT CURRENT_TIMESTAMP);
      CREATE TABLE review_assignments(item_id INTEGER REFERENCES review_items(id),reviewer_id INTEGER REFERENCES users(id),PRIMARY KEY(item_id,reviewer_id));
      CREATE INDEX idx_sessions_student ON sim_sessions(student_id,finished);
      CREATE INDEX idx_measurements_session ON sim_measurements(session_id,group_id,slot);
      CREATE INDEX idx_abnormals_session ON sim_abnormals(session_id);
      CREATE INDEX idx_events_session ON sim_events(session_id);
      CREATE INDEX idx_users_class ON users(class_id);
      CREATE INDEX idx_attempts_student ON quiz_attempts(student_id,quiz_id);
      CREATE INDEX idx_submissions_student ON project_submissions(student_id,project_id);
      CREATE INDEX idx_reviews_item ON reviews(item_id);
      CREATE INDEX idx_notifications_student ON notifications(student_id,id);
      CREATE UNIQUE INDEX idx_measurements_event ON sim_measurements(event_id) WHERE event_id IS NOT NULL;
      CREATE UNIQUE INDEX idx_abnormals_event ON sim_abnormals(event_id) WHERE event_id IS NOT NULL;
    `);
    const first=db.prepare("SELECT id FROM users WHERE role='teacher' ORDER BY id LIMIT 1").get();
    if(first)db.prepare('UPDATE classes SET teacher_id=? WHERE teacher_id IS NULL').run(first.id);
    db.exec("UPDATE sim_sessions SET demo=1 WHERE unity_session LIKE 'demo-%'; UPDATE sim_sessions SET status=CASE WHEN finished=1 THEN 'finished' ELSE 'active' END");
    for(const q of db.prepare("SELECT * FROM questions WHERE typeof(score)='text'").all())db.prepare('UPDATE questions SET analysis=score,score=2,tags=? WHERE id=?').run(q.type==='fill'?'公式':'基础',q.id);
    db.exec("UPDATE questions SET analysis='请结合课程原理和题目选项核对。' WHERE analysis IS NULL; UPDATE reviews SET snapshot=COALESCE((SELECT dimensions FROM rubrics WHERE id=reviews.rubric_id),'[]')");
    const {gradeAnswers}=require('./quiz');
    for(const a of db.prepare('SELECT * FROM quiz_attempts').all()){
      const q=db.prepare('SELECT * FROM quizzes WHERE id=?').get(a.quiz_id);if(!q)continue;
      const questions=JSON.parse(q.question_ids||'[]').map(id=>db.prepare('SELECT * FROM questions WHERE id=?').get(id)).filter(Boolean);
      const result=gradeAnswers(questions,JSON.parse(a.answers||'{}'));
      db.prepare('UPDATE quiz_attempts SET snapshot=?,detail=?,score=?,total=?,status=? WHERE id=?').run(JSON.stringify(questions),JSON.stringify(result.detail),result.score,result.total,result.status,a.id);
    }
    const bcrypt=require('bcryptjs');
    for(const u of db.prepare('SELECT id,password FROM users').all())if(bcrypt.compareSync('123456',u.password))db.prepare('UPDATE users SET must_change=1 WHERE id=?').run(u.id);
    db.prepare('INSERT INTO migrations(version) VALUES(140)').run();db.exec('COMMIT');
  }catch(e){db.exec('ROLLBACK');throw e;}
};
