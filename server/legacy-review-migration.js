module.exports=db=>{
  if(db.prepare('SELECT 1 FROM migrations WHERE version=143').get())return;
  db.exec('BEGIN IMMEDIATE');try{
    const items=db.prepare('SELECT i.*,u.class_id,c.teacher_id FROM review_items i JOIN users u ON u.id=i.student_id JOIN classes c ON c.id=u.class_id').all();
    for(const item of items){
      const previous=db.prepare('SELECT DISTINCT rubric_id FROM reviews WHERE item_id=?').all(item.id);
      const rubric=item.rubric_id?db.prepare('SELECT * FROM rubrics WHERE id=?').get(item.rubric_id):previous.length===1?db.prepare('SELECT * FROM rubrics WHERE id=? AND target_type=? AND owner_id=?').get(previous[0].rubric_id,item.item_type,item.teacher_id):previous.length===0?db.prepare('SELECT * FROM rubrics WHERE target_type=? AND owner_id=? AND archived=0 ORDER BY id LIMIT 1').get(item.item_type,item.teacher_id):null;
      if(!rubric){db.prepare('INSERT INTO audit_log(action,target) VALUES(?,?)').run('legacy-rubric-needs-review',String(item.id));continue;}
      if(!item.rubric_id)db.prepare('UPDATE review_items SET rubric_id=? WHERE id=?').run(rubric.id,item.id);
      const priorPeers=db.prepare("SELECT r.reviewer_id FROM reviews r JOIN users u ON u.id=r.reviewer_id WHERE r.item_id=? AND u.class_id=? AND u.role='student' AND u.id<>?").all(item.id,item.class_id,item.student_id);
      for(const p of priorPeers)db.prepare('INSERT OR IGNORE INTO review_assignments VALUES(?,?)').run(item.id,p.reviewer_id);
      const count=db.prepare('SELECT COUNT(*) n FROM review_assignments WHERE item_id=?').get(item.id).n;
      const peers=db.prepare("SELECT u.id,COUNT(a.item_id) load FROM users u LEFT JOIN review_assignments a ON a.reviewer_id=u.id WHERE u.class_id=? AND u.id<>? AND u.role='student' AND u.active=1 AND NOT EXISTS(SELECT 1 FROM review_assignments x WHERE x.item_id=? AND x.reviewer_id=u.id) GROUP BY u.id ORDER BY load,u.id LIMIT ?").all(item.class_id,item.student_id,item.id,Math.max(0,2-count));
      for(const p of peers)db.prepare('INSERT INTO review_assignments VALUES(?,?)').run(item.id,p.id);
      db.prepare('INSERT INTO audit_log(action,target,after_json) VALUES(?,?,?)').run('legacy-review-migrated',String(item.id),JSON.stringify({rubric_id:rubric.id,retainedReviewers:priorPeers,added:peers.map(p=>p.id)}));
    }
    db.prepare('INSERT INTO migrations(version) VALUES(143)').run();db.exec('COMMIT');
  }catch(e){db.exec('ROLLBACK');throw e;}
};
