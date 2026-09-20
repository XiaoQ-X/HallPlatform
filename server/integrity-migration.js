module.exports=db=>{
  if(db.prepare('SELECT 1 FROM migrations WHERE version=142').get())return;
  db.exec('BEGIN IMMEDIATE');
  try{
    const relations={users:{class_id:'classes'},sim_sessions:{student_id:'users'},sim_events:{session_id:'sim_sessions'},sim_measurements:{session_id:'sim_sessions'},sim_abnormals:{session_id:'sim_sessions'},quiz_attempts:{student_id:'users',quiz_id:'quizzes'},project_submissions:{student_id:'users',project_id:'projects'},cleaning_records:{student_id:'users',session_id:'sim_sessions',measurement_id:'sim_measurements'},uncertainty_records:{student_id:'users'},analysis_records:{student_id:'users'},review_items:{student_id:'users',session_id:'sim_sessions',rubric_id:'rubrics',submission_id:'project_submissions'},reviews:{item_id:'review_items',reviewer_id:'users',rubric_id:'rubrics'},appeals:{student_id:'users'},notifications:{student_id:'users'},grade_overrides:{student_id:'users'}};
    for(const [table,columns]of Object.entries(relations))for(const [column,parent]of Object.entries(columns)){
      const invalid=db.prepare(`SELECT id FROM ${table} WHERE ${column} IS NOT NULL AND NOT EXISTS(SELECT 1 FROM ${parent} WHERE id=${table}.${column})`).all();
      if(invalid.length)db.prepare('INSERT INTO audit_log(action,target,after_json) VALUES(?,?,?)').run('migration-orphan-review',table+'.'+column,JSON.stringify(invalid));
      for(const op of ['INSERT','UPDATE'])db.exec(`CREATE TRIGGER integrity_${table}_${column}_${op} BEFORE ${op} ON ${table} WHEN NEW.${column} IS NOT NULL AND NOT EXISTS(SELECT 1 FROM ${parent} WHERE id=NEW.${column}) BEGIN SELECT RAISE(ABORT,'invalid parent reference'); END`);
      db.exec(`CREATE TRIGGER restrict_${parent}_${table}_${column} BEFORE DELETE ON ${parent} WHEN EXISTS(SELECT 1 FROM ${table} WHERE ${column}=OLD.id) BEGIN SELECT RAISE(ABORT,'referenced record cannot be deleted'); END`);
      db.exec(`CREATE INDEX IF NOT EXISTS idx_integrity_${table}_${column} ON ${table}(${column})`);
    }
    for(const [table,columns,filter]of [['project_submissions','student_id,project_id','1'],['reviews','item_id,reviewer_id','1'],['grade_overrides','student_id,component','1'],['users','class_id,student_no',"role='student' AND student_no IS NOT NULL AND student_no<>''"]]){
      const duplicates=db.prepare(`SELECT ${columns},COUNT(*) n FROM ${table} WHERE ${filter} GROUP BY ${columns} HAVING n>1`).all();
      if(duplicates.length)db.prepare('INSERT INTO audit_log(action,target,after_json) VALUES(?,?,?)').run('migration-duplicate-review',table,JSON.stringify(duplicates));
      else db.exec(`CREATE UNIQUE INDEX integrity_unique_${table} ON ${table}(${columns}) WHERE ${filter}`);
    }
    for(const [table,column,min,max]of [['project_submissions','grade',0,100],['sim_sessions','quality_score',0,100],['grade_overrides','score',0,100]])for(const op of ['INSERT','UPDATE'])db.exec(`CREATE TRIGGER bound_${table}_${op} BEFORE ${op} ON ${table} WHEN NEW.${column} IS NOT NULL AND (typeof(NEW.${column}) NOT IN ('real','integer') OR NEW.${column}<${min} OR NEW.${column}>${max}) BEGIN SELECT RAISE(ABORT,'invalid score'); END`);
    db.prepare('INSERT INTO migrations(version) VALUES(142)').run();db.exec('COMMIT');
  }catch(e){db.exec('ROLLBACK');throw e;}
};
