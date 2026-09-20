module.exports=db=>{
 if(db.prepare('SELECT 1 FROM migrations WHERE version=144').get())return;
 db.exec('BEGIN IMMEDIATE');try{
  db.exec("ALTER TABLE review_items ADD COLUMN evidence TEXT NOT NULL DEFAULT '{}'; ALTER TABLE review_items ADD COLUMN source_version INTEGER NOT NULL DEFAULT 1; ALTER TABLE project_progress ADD COLUMN template_hash TEXT;");
  db.prepare('INSERT INTO migrations(version) VALUES(144)').run();db.exec('COMMIT');
 }catch(e){db.exec('ROLLBACK');throw e;}
};
