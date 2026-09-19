module.exports = (jwt, SECRET, auth) => {
  const r = require('express').Router();
  const db = require('../db');
  const bcrypt = require('bcryptjs');

  r.post('/login', (req, res) => {
    const { username, password } = req.body;
    const u = db.prepare('SELECT * FROM users WHERE username=?').get(username);
    if (!u || !bcrypt.compareSync(password || '', u.password))
      return res.status(400).json({ error: '用户名或密码错误' });
    const token = jwt.sign({ id: u.id, role: u.role, name: u.name }, SECRET, { expiresIn: '7d' });
    res.json({ token, role: u.role, name: u.name });
  });

  r.post('/register', (req, res) => {
    const { username, password, name, class_id, student_no } = req.body;
    if (!username || !password) return res.status(400).json({ error: '请填写账号密码' });
    const exist = db.prepare('SELECT id FROM users WHERE username=?').get(username);
    if (exist) return res.status(400).json({ error: '账号已存在' });
    let cid = class_id;
    if (!cid) cid = db.prepare('SELECT id FROM classes LIMIT 1').get()?.id;
    const hash = bcrypt.hashSync(password, 8);
    const info = db.prepare('INSERT INTO users(username,password,name,role,class_id,student_no,avatar) VALUES(?,?,?,?,?,?,?)')
      .run(username, hash, name || username, 'student', cid, student_no, '🧑‍🎓');
    const token = jwt.sign({ id: info.lastInsertRowid, role: 'student', name: name || username }, SECRET, { expiresIn: '7d' });
    res.json({ token, role: 'student', name: name || username });
  });

  r.get('/classes', (req, res) => {
    res.json(db.prepare('SELECT id,name,code FROM classes').all());
  });

  // 更新个人资料：头像 / 姓名；修改密码需校验旧密码
  r.put('/me', auth, (req, res) => {
    const u = db.prepare('SELECT * FROM users WHERE id=?').get(req.user.id);
    if (!u) return res.status(404).json({ error: '用户不存在' });
    const { name, avatar, oldPassword, newPassword } = req.body;
    if (name != null && !String(name).trim()) return res.status(400).json({ error: '姓名不能为空' });
    db.prepare('UPDATE users SET name=COALESCE(?,name), avatar=COALESCE(?,avatar) WHERE id=?')
      .run(name != null ? String(name).trim() : null, avatar != null ? avatar : null, u.id);
    if (newPassword) {
      if (!oldPassword || !bcrypt.compareSync(oldPassword, u.password))
        return res.status(400).json({ error: '原密码不正确' });
      if (String(newPassword).length < 6)
        return res.status(400).json({ error: '新密码至少 6 位' });
      db.prepare('UPDATE users SET password=? WHERE id=?')
        .run(bcrypt.hashSync(String(newPassword), 8), u.id);
    }
    const fresh = db.prepare(`SELECT u.id,u.username,u.name,u.role,u.avatar,u.student_no,c.name class_name
      FROM users u LEFT JOIN classes c ON c.id=u.class_id WHERE u.id=?`).get(u.id);
    res.json({ user: fresh });
  });

  return r;
};
