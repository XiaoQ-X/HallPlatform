const { DatabaseSync } = require('node:sqlite');
const path = require('path');
const fs = require('fs');

const dataDir = path.join(__dirname, '..', 'data');
if (!fs.existsSync(dataDir)) fs.mkdirSync(dataDir, { recursive: true });
const db = new DatabaseSync(path.join(dataDir, 'hall.db'));
db.exec('PRAGMA journal_mode = WAL');
db.exec('PRAGMA foreign_keys = ON');

db.exec(`
CREATE TABLE IF NOT EXISTS classes (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT UNIQUE, code TEXT, created_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS users (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  username TEXT UNIQUE, password TEXT, name TEXT,
  role TEXT CHECK(role IN ('student','teacher')) DEFAULT 'student',
  class_id INTEGER, student_no TEXT, avatar TEXT,
  created_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS cases (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  title TEXT, subtitle TEXT, cover TEXT, color TEXT,
  category TEXT, difficulty TEXT, tags TEXT,
  content TEXT, sim_config TEXT, published INTEGER DEFAULT 1,
  sort INTEGER DEFAULT 0, views INTEGER DEFAULT 0,
  created_at TEXT DEFAULT (datetime('now','localtime')),
  updated_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS ideology (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  title TEXT, subtitle TEXT, cover TEXT, color TEXT,
  category TEXT, source TEXT, content TEXT, published INTEGER DEFAULT 1,
  sort INTEGER DEFAULT 0, views INTEGER DEFAULT 0,
  created_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS questions (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  type TEXT, stem TEXT, options TEXT, answer TEXT, analysis TEXT,
  score REAL DEFAULT 2, tags TEXT, published INTEGER DEFAULT 1,
  created_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS quizzes (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  title TEXT, description TEXT, time_minutes INTEGER DEFAULT 20,
  question_ids TEXT, published INTEGER DEFAULT 1,
  created_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS quiz_attempts (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  quiz_id INTEGER, student_id INTEGER,
  answers TEXT, score REAL, total REAL,
  started_at TEXT, submitted_at TEXT
);
CREATE TABLE IF NOT EXISTS projects (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  title TEXT, subtitle TEXT, cover TEXT, color TEXT, category TEXT,
  guide TEXT, tasks TEXT, attachments TEXT, published INTEGER DEFAULT 1,
  sort INTEGER DEFAULT 0, created_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS project_submissions (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  project_id INTEGER, student_id INTEGER,
  content TEXT, files TEXT, submitted_at TEXT,
  grade REAL, feedback TEXT, graded_at TEXT
);
CREATE TABLE IF NOT EXISTS sim_sessions (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  student_id INTEGER, case_id TEXT, unity_session TEXT,
  started_at TEXT DEFAULT (datetime('now','localtime')),
  finished_at TEXT, finished INTEGER DEFAULT 0, state TEXT
);
CREATE TABLE IF NOT EXISTS sim_events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  session_id INTEGER, type TEXT, event_id TEXT UNIQUE,
  payload TEXT, received_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS sim_measurements (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  session_id INTEGER, group_id TEXT, slot INTEGER,
  is_dir INTEGER, im_dir INTEGER, v_dir INTEGER,
  IS_mA REAL, IM_A REAL, B_T REAL, raw_mV REAL,
  VH_mV REAL, normVH_mV REAL, group_complete INTEGER,
  payload TEXT, measured_at TEXT
);
CREATE TABLE IF NOT EXISTS sim_abnormals (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  session_id INTEGER, code TEXT, detail TEXT,
  success INTEGER, step INTEGER, payload TEXT,
  occurred_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS cleaning_records (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  student_id INTEGER, session_id INTEGER,
  label TEXT, point_label TEXT, abnormal_code TEXT,
  cause TEXT, action TEXT, resolved INTEGER DEFAULT 0,
  created_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS uncertainty_records (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  student_id INTEGER, title TEXT, inputs TEXT, result TEXT,
  created_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS analysis_records (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  student_id INTEGER, title TEXT, model TEXT,
  dataset TEXT, params TEXT, metrics TEXT,
  created_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS rubrics (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  title TEXT, target_type TEXT, dimensions TEXT,
  created_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS review_items (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  student_id INTEGER, item_type TEXT, title TEXT, summary TEXT,
  recording TEXT, files TEXT, session_id INTEGER,
  created_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS reviews (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  item_id INTEGER, reviewer_id INTEGER, rubric_id INTEGER,
  scores TEXT, comment TEXT,
  created_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS appeals (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  ref_type TEXT, ref_id INTEGER, student_id INTEGER,
  content TEXT, reply TEXT, status TEXT DEFAULT '待处理',
  created_at TEXT DEFAULT (datetime('now','localtime')), replied_at TEXT
);
CREATE TABLE IF NOT EXISTS pushes (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  teacher_id INTEGER, target_type TEXT, target_id INTEGER,
  title TEXT, message TEXT, resource_type TEXT, resource_id INTEGER,
  created_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS notifications (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  student_id INTEGER, title TEXT, content TEXT, link TEXT,
  read INTEGER DEFAULT 0, created_at TEXT DEFAULT (datetime('now','localtime'))
);
CREATE TABLE IF NOT EXISTS grade_overrides (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  student_id INTEGER, component TEXT, ref_id INTEGER,
  score REAL, max_score REAL,
  created_at TEXT DEFAULT (datetime('now','localtime'))
);
`);

// 统一兜底：位置参数中的 undefined 无法被 node:sqlite 绑定，自动转 null
const _Statement = db.prepare('SELECT 1').constructor;
const _run = _Statement.prototype.run;
_Statement.prototype.run = function (...args) {
  return _run.call(this, ...args.map(a => (a === undefined ? null : a)));
};

module.exports = db;
