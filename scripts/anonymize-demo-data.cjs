const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { DatabaseSync } = require('node:sqlite');

// This is an explicit operation on the demonstration database, not an automatic
// production migration. IDs, login credentials, memberships and scores stay put.
function anonymize(db) {
  db.exec('BEGIN IMMEDIATE');
  try {
    db.exec(`
      CREATE TABLE IF NOT EXISTS presentation_settings(key TEXT PRIMARY KEY,value TEXT NOT NULL);
      CREATE TABLE IF NOT EXISTS presentation_user_aliases(user_id INTEGER PRIMARY KEY,role TEXT NOT NULL,ordinal INTEGER NOT NULL,UNIQUE(role,ordinal));
      CREATE TABLE IF NOT EXISTS presentation_class_aliases(class_id INTEGER PRIMARY KEY,ordinal INTEGER NOT NULL UNIQUE);
    `);
    const users = db.prepare('SELECT id,name,role,student_no,class_id FROM users ORDER BY id').all();
    const classes = db.prepare('SELECT id,name,code FROM classes ORDER BY id').all();
    const aliases = new Map(db.prepare('SELECT * FROM presentation_user_aliases').all().map(x => [x.user_id, x]));
    const classAliases = new Map(db.prepare('SELECT * FROM presentation_class_aliases').all().map(x => [x.class_id, x]));
    const next = { student: 0, teacher: 0 };
    for (const a of aliases.values()) next[a.role] = Math.max(next[a.role] || 0, a.ordinal);
    let nextClass = Math.max(0, ...Array.from(classAliases.values(), a => a.ordinal));
    for (const u of users) if (!aliases.has(u.id)) {
      const alias = { user_id: u.id, role: u.role, ordinal: ++next[u.role] };
      db.prepare('INSERT INTO presentation_user_aliases(user_id,role,ordinal) VALUES(?,?,?)').run(u.id, u.role, alias.ordinal);
      aliases.set(u.id, alias);
    }
    for (const c of classes) if (!classAliases.has(c.id)) {
      const alias = { class_id: c.id, ordinal: ++nextClass };
      db.prepare('INSERT INTO presentation_class_aliases(class_id,ordinal) VALUES(?,?)').run(c.id, alias.ordinal);
      classAliases.set(c.id, alias);
    }
    const className = id => classAliases.has(id) ? '班级' + classAliases.get(id).ordinal : null;
    const identity = id => {
      const u = users.find(x => x.id === Number(id)), a = aliases.get(Number(id));
      return u && a ? { name: (u.role === 'student' ? '同学' : '教师') + a.ordinal, student_no: u.role === 'student' ? String(a.ordinal) : null, class_name: className(u.class_id) } : null;
    };
    const substitutions = new Map();
    const add = (old, value) => {
      if (typeof old !== 'string' || old.length < 2 || old === value) return;
      substitutions.set(old, substitutions.has(old) && substitutions.get(old) !== value ? '匿名同学' : value);
    };
    for (const u of users) {
      const p = identity(u.id);
      if (!/^(同学|教师)\d+$/.test(u.name || '')) add(u.name, p.name);
      // Short numeric values may be measurements and must never be rewritten.
      if (u.role === 'student' && /^\d{6,}$/.test(u.student_no || '')) add(u.student_no, p.student_no);
    }
    for (const c of classes) if (!/^班级\d+$/.test(c.name || '')) add(c.name, className(c.id));
    const escapes = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const patterns = [...substitutions].sort((a, b) => b[0].length - a[0].length).map(([from, to]) => [new RegExp(/^\d+$/.test(from) ? '(?<!\\d)' + escapes(from) + '(?!\\d)' : escapes(from), 'g'), to]);
    const prose = value => typeof value === 'string' ? patterns.reduce((s, [re, replacement]) => s.replace(re, () => replacement), value) : value;

    // Free-form human text is scrubbed for exact previously stored identities.
    // Measurements, simulation payloads, answers and mathematical inputs are not
    // traversed or string-replaced.
    const textFields = {
      notifications: ['title', 'content'], pushes: ['title', 'message'],
      review_items: ['title', 'summary'], reviews: ['comment'], appeals: ['content', 'reply'],
      project_submissions: ['content', 'feedback'], side_effect_records: ['title', 'conclusion'],
      side_effect_grades: ['comment'], cleaning_records: ['label', 'point_label', 'cause', 'action'],
      uncertainty_records: ['title'], analysis_records: ['title']
    };
    let textRows = 0, snapshotRows = 0;
    for (const [table, fields] of Object.entries(textFields)) {
      if (!db.prepare("SELECT 1 FROM sqlite_master WHERE type='table' AND name=?").get(table)) continue;
      const columns = new Set(db.prepare(`PRAGMA table_info(${table})`).all().map(c => c.name));
      const present = fields.filter(f => columns.has(f));
      for (const row of db.prepare(`SELECT id,${present.join(',')} FROM ${table}`).all()) {
        const changed = present.filter(f => prose(row[f]) !== row[f]);
        if (changed.length) {
          db.prepare(`UPDATE ${table} SET ${changed.map(f => f + '=?').join(',')} WHERE id=?`).run(...changed.map(f => prose(row[f])), row.id);
          textRows++;
        }
      }
    }
    function snapshot(value, userId) {
      if (!value || typeof value !== 'object') return value;
      if (Array.isArray(value)) return value.map(x => snapshot(x, userId));
      const out = { ...value };
      const own = identity(value.student_id || userId);
      for (const key of Object.keys(out)) {
        if (['name', 'student_name', 'owner_name', 'reviewer_name', 'teacher_name', 'class_name'].includes(key)) out[key] = prose(out[key]);
        else if (['student_no', 'no'].includes(key) && own && typeof out[key] === 'string') out[key] = own.student_no;
        else if (['content', 'feedback', 'reason', 'comment', 'reply', 'summary'].includes(key)) out[key] = prose(out[key]);
        else if (out[key] && typeof out[key] === 'object') out[key] = snapshot(out[key], value.student_id || userId);
      }
      // Some audit/grade snapshots retain login names. No page needs these to
      // authenticate and usernames remain unchanged in the users table.
      if ('username' in out) delete out.username;
      return out;
    }
    const snapshotFields = { grade_publications: ['payload'], appeals: ['snapshot'], submission_versions: ['payload'], audit_log: ['before_json', 'after_json'] };
    for (const [table, fields] of Object.entries(snapshotFields)) {
      if (!db.prepare("SELECT 1 FROM sqlite_master WHERE type='table' AND name=?").get(table)) continue;
      const cols = new Set(db.prepare(`PRAGMA table_info(${table})`).all().map(c => c.name));
      const context = cols.has('student_id') ? 'student_id' : table === 'audit_log' ? 'target' : 'NULL';
      for (const row of db.prepare(`SELECT id,${context} AS identity_id,${fields.join(',')} FROM ${table}`).all()) {
        for (const field of fields) {
          let parsed; try { parsed = JSON.parse(row[field]); } catch { continue; }
          const updated = JSON.stringify(snapshot(parsed, row.identity_id));
          if (updated !== row[field]) {
            db.prepare(`UPDATE ${table} SET ${field}=? WHERE id=?`).run(updated, row.id);
            snapshotRows++;
          }
        }
      }
    }

    // Temporary values avoid UNIQUE conflicts (e.g. an existing "班级1", or a
    // student whose old number is another student's new demonstration number).
    const nonce = crypto.randomUUID();
    for (const u of users) if (u.role === 'student') db.prepare('UPDATE users SET student_no=? WHERE id=?').run('anonymous-' + nonce + '-' + u.id, u.id);
    for (const c of classes) db.prepare('UPDATE classes SET name=? WHERE id=?').run('anonymous-' + nonce + '-' + c.id, c.id);
    for (const u of users) {
      const p = identity(u.id);
      db.prepare('UPDATE users SET name=?,student_no=? WHERE id=?').run(p.name, p.student_no, u.id);
    }
    for (const c of classes) db.prepare('UPDATE classes SET name=?,code=? WHERE id=?').run(className(c.id), 'CLASS' + classAliases.get(c.id).ordinal, c.id);
    db.prepare("INSERT INTO presentation_settings(key,value) VALUES('anonymous_identity','1') ON CONFLICT(key) DO UPDATE SET value=excluded.value").run();
    db.exec('COMMIT');
    return { students: users.filter(u => u.role === 'student').length, teachers: users.filter(u => u.role === 'teacher').length, classes: classes.length, textRows, snapshotRows, credentialsChanged: false };
  } catch (error) {
    db.exec('ROLLBACK');
    throw error;
  }
}

function main() {
  const root = path.resolve(__dirname, '..');
  const data = path.resolve(process.env.HALL_DATA_DIR || path.join(root, 'data'));
  const backup = path.resolve(process.argv[2] || path.join(root, 'backups', 'before-anonymous-' + new Date().toISOString().replace(/[:.]/g, '-')));
  if (!fs.existsSync(path.join(data, 'hall.db'))) throw Error('指定目录中不存在 hall.db');
  if (fs.existsSync(backup)) throw Error('备份目标必须为不存在的新目录');
  fs.mkdirSync(backup, { recursive: true, mode: 0o700 });
  const db = new DatabaseSync(path.join(data, 'hall.db'));
  db.exec('PRAGMA foreign_keys=ON; PRAGMA busy_timeout=10000');
  try {
    const backupFile = path.join(backup, 'hall.db');
    db.exec("VACUUM INTO '" + backupFile.replace(/'/g, "''") + "'");
    fs.chmodSync(backupFile, 0o600);
    const result = anonymize(db);
    fs.writeFileSync(path.join(backup, 'manifest.json'), JSON.stringify({ created: new Date().toISOString(), backupSha256: crypto.createHash('sha256').update(fs.readFileSync(backupFile)).digest('hex'), result }, null, 2), { mode: 0o600 });
    console.log(JSON.stringify({ backup, ...result }, null, 2));
  } finally { db.close(); }
}

if (require.main === module) main();
module.exports = { anonymize };
