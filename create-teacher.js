const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const bcrypt = require('bcryptjs');
const db = require('./server/db');
require('./server/content-migration')(db);
const [username, name, option] = process.argv.slice(2);
if (!username || username.length > 80 || !name || name.length > 80 || (option && option !== '--reset')) {
  console.error('用法: node create-teacher.js <账号> <姓名> [--reset]。随机密码写入 data/initial-access.txt。');
  process.exit(1);
}
const old = db.prepare('SELECT * FROM users WHERE username=?').get(username);
if (old && (option !== '--reset' || old.role !== 'teacher')) {
  console.error('账号已存在；仅可使用 --reset 重置教师账号。');
  process.exit(1);
}
const password = crypto.randomBytes(24).toString('base64url');
db.exec('BEGIN IMMEDIATE');
try {
  if (old) db.prepare('UPDATE users SET password=?,must_change=0,active=1,token_version=token_version+1 WHERE id=?').run(bcrypt.hashSync(password,12),old.id);
  else db.prepare("INSERT INTO users(username,password,name,role,avatar) VALUES(?,?,?,'teacher','教师')").run(username,bcrypt.hashSync(password,12),name);
  const teacherId=db.prepare('SELECT id FROM users WHERE username=?').get(username).id;
  if(db.prepare("SELECT COUNT(*) n FROM users WHERE role='teacher'").get().n===1){
    for(const table of ['classes','cases','ideology','questions','quizzes','projects','rubrics']){
      const column=table==='classes'?'teacher_id':'owner_id';db.prepare(`UPDATE ${table} SET ${column}=? WHERE ${column} IS NULL`).run(teacherId);
    }
  }
  db.prepare('INSERT INTO audit_log(actor_id,action,target) VALUES(?,?,?)').run(teacherId,old?'admin-password-reset':'admin-create',username);
  const dir=process.env.HALL_DATA_DIR||path.join(__dirname,'data');
  fs.writeFileSync(path.join(dir,'initial-access.txt'),`本机教师账号：${username}\n初始密码：${password}\n请登录后在个人资料中修改密码，并删除此凭据文件。\n`,{mode:0o600});
  db.exec('COMMIT');console.log('账号已就绪；凭据文件：'+path.join(dir,'initial-access.txt'));
}catch(e){db.exec('ROLLBACK');throw e;}finally{db.close();}
