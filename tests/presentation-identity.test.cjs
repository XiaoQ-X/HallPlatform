const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

const work = path.resolve(__dirname, '../work');
fs.mkdirSync(work, { recursive: true });
process.env.HALL_DATA_DIR = fs.mkdtempSync(path.join(work, 'presentation-'));
process.env.HALL_UPLOAD_DIR = path.join(process.env.HALL_DATA_DIR, 'uploads');
process.env.HALL_DEMO = '1';
process.env.HALL_JWT_SECRET = crypto.randomBytes(48).toString('hex');

const app = require('../server');
const db = require('../server/db');
const bcrypt = require('bcryptjs');
const { anonymize } = require('../scripts/anonymize-demo-data.cjs');
const password = 'Presentation-Test-2026!';
db.prepare('UPDATE users SET password=?,must_change=0').run(bcrypt.hashSync(password, 4));

function request(base, route, token, body, method) {
  return fetch(base + '/api' + route, {
    method: method || (body ? 'POST' : 'GET'),
    headers: { ...(token ? { Authorization: 'Bearer ' + token } : {}), ...(body ? { 'Content-Type': 'application/json' } : {}) },
    body: body ? JSON.stringify(body) : undefined
  }).then(async r => ({ status: r.status, data: await r.json().catch(() => null) }));
}

test('anonymous presentation migration is idempotent and hides login identifiers', async t => {
  const first = anonymize(db);
  assert.equal(first.credentialsChanged, false);
  const second = anonymize(db);
  assert.equal(second.textRows, 0);
  assert.equal(second.snapshotRows, 0);
  assert.deepEqual(db.prepare('SELECT user_id,ordinal FROM presentation_user_aliases ORDER BY user_id').all().map(x => ({ user_id: x.user_id, ordinal: x.ordinal })),
    [{ user_id: 1, ordinal: 1 }, { user_id: 2, ordinal: 1 }, { user_id: 3, ordinal: 2 }, { user_id: 4, ordinal: 3 }, { user_id: 5, ordinal: 4 }, { user_id: 6, ordinal: 5 }, { user_id: 7, ordinal: 6 }]);

  const server = app.listen(0, '127.0.0.1');
  await new Promise(resolve => server.once('listening', resolve));
  const base = 'http://127.0.0.1:' + server.address().port;
  try {
    const teacherLogin = await request(base, '/auth/login', null, { username: 'teacher', password });
    const studentLogin = await request(base, '/auth/login', null, { username: 'student', password });
    assert.equal(teacherLogin.status, 200); assert.equal(studentLogin.status, 200);
    const teacher = teacherLogin.data.token, student = studentLogin.data.token;

    await t.test('me and profile update never return the login username', async () => {
      const me = await request(base, '/me', teacher);
      assert.equal(me.status, 200); assert.equal(me.data.name, '教师1'); assert.equal('username' in me.data, false);
      const put = await request(base, '/auth/me', teacher, { name: '真实姓名不应写入', avatar: '🧑‍🏫' }, 'PUT');
      assert.equal(put.status, 200); assert.equal(put.data.user.name, '教师1'); assert.equal('username' in put.data.user, false);
      const studentMe = await request(base, '/me', student);
      assert.equal(studentMe.data.name, '同学1'); assert.equal('username' in studentMe.data, false);
    });
    await t.test('teacher list hides username and supports anonymous create/edit', async () => {
      const before = await request(base, '/teacher/students', teacher);
      assert.equal(before.status, 200); assert(before.data.every(s => !('username' in s) && /^学生账号\d+$/.test(s.account_label)));
      const classes = await request(base, '/auth/classes', teacher);
      const createdClass = await request(base, '/teacher/classes', teacher, { name: '真实班级名', code: 'REAL' });
      assert.equal(createdClass.status, 200);
      const allClasses = await request(base, '/auth/classes', teacher);
      assert(allClasses.data.some(c => c.name === '班级2' && c.code === 'CLASS2'));
      const created = await request(base, '/teacher/students', teacher, { username: 'new-login', password: 'new-pass-2026!', name: '真实姓名', student_no: '999999', class_id: classes.data[0].id, avatar: '🧑' });
      assert.equal(created.status, 200);
      const id = created.data.id;
      const row = (await request(base, '/teacher/students', teacher)).data.find(s => s.id === id);
      assert.equal(row.name, '同学7'); assert.equal(row.student_no, '7'); assert.equal(row.account_label, '学生账号7'); assert.equal('username' in row, false);
      const edited = await request(base, '/teacher/students/' + id, teacher, { username: 'new-login', password: 'ignored', name: '又一个真实姓名', student_no: '888888', class_id: classes.data[0].id, avatar: '🧑' }, 'PUT');
      assert.equal(edited.status, 200);
      const rowAfter = (await request(base, '/teacher/students', teacher)).data.find(s => s.id === id);
      assert.equal(rowAfter.name, '同学7'); assert.equal(rowAfter.student_no, '7');
    });
    await t.test('missing alias repairs from existing anonymous student number', async () => {
      const id = db.prepare("SELECT id FROM users WHERE username='li'").get().id;
      db.prepare('DELETE FROM presentation_user_aliases WHERE user_id=?').run(id);
      const c = db.prepare('SELECT class_id FROM users WHERE id=?').get(id).class_id;
      const liLogin = await request(base, '/auth/login', null, { username: 'li', password });
      assert.equal(liLogin.data.name, '同学待分配');
      const liMe = await request(base, '/me', liLogin.data.token);
      assert.equal(liMe.data.student_no, null); assert.equal(liMe.data.name, '同学待分配'); assert.equal('username' in liMe.data, false);
      const pendingRow = (await request(base, '/teacher/students', teacher)).data.find(s => s.id === id);
      assert.equal(pendingRow.student_no, null); assert.equal(pendingRow.name, '同学待分配'); assert.equal('username' in pendingRow, false);
      const edited = await request(base, '/teacher/students/' + id, teacher, { name: 'leak', student_no: '123456', class_id: c, avatar: '🧑' }, 'PUT');
      assert.equal(edited.status, 200);
      const alias = db.prepare('SELECT ordinal FROM presentation_user_aliases WHERE user_id=?').get(id);
      assert.equal(alias.ordinal, 2);
      const row = (await request(base, '/teacher/students', teacher)).data.find(s => s.id === id);
      assert.equal(row.name, '同学2'); assert.equal(row.student_no, '2'); assert.equal('username' in row, false);
    });
  } finally { await new Promise(resolve => server.close(resolve)); db.close(); }
});
