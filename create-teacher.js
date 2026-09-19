// 教师账号创建脚本（教师账号不开放公开注册，须由管理员在服务器执行）
// 用法： node create-teacher.js <账号> <密码> <姓名>
// 示例： node create-teacher.js liu_teacher mypass 刘老师
const path = require('path');
const db = require(path.join(__dirname, 'server', 'db'));
const bcrypt = require('bcryptjs');

const [username, password, name] = process.argv.slice(2);
if (!username || !password) {
  console.log('用法: node create-teacher.js <账号> <密码> [姓名]');
  process.exit(1);
}
const exist = db.prepare('SELECT id FROM users WHERE username=?').get(username);
if (exist) {
  console.log('账号已存在，无法重复创建：', username);
  process.exit(1);
}
const hash = bcrypt.hashSync(password, 8);
const info = db.prepare('INSERT INTO users(username,password,name,role,avatar) VALUES(?,?,?,?,?)')
  .run(username, hash, name || username, 'teacher', '👨‍🏫');
console.log('教师账号创建成功：', { id: info.lastInsertRowid, username, name: name || username });
console.log('现在可使用该账号在登录页登录，密码为你设置的密码。');
