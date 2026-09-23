// Presentation mode is enabled explicitly by scripts/anonymize-demo-data.cjs.
// Authentication continues to use the existing username and password in users.
function enabled(db) {
  if (!db.prepare("SELECT 1 FROM sqlite_master WHERE type='table' AND name='presentation_settings'").get()) return false;
  return db.prepare("SELECT value FROM presentation_settings WHERE key='anonymous_identity'").get()?.value === '1';
}

function profile(db, user) {
  if (!user || !enabled(db)) return user;
  const { username, ...visible } = user;
  return { ...visible, identity_anonymized: true };
}

// Keep authentication identifiers server-side. The public profile never needs
// the login username, and an absent alias should remain explicit rather than
// accidentally becoming another person's ordinal.
function visibleProfile(db, user) {
  if (!user) return user;
  if (!enabled(db)) return user;
  const a = alias(db, user.id);
  const classAlias = user.class_id ? db.prepare('SELECT ordinal FROM presentation_class_aliases WHERE class_id=?').get(user.class_id) : null;
  const visible = {
    ...user,
    name: (user.role === 'student' ? '同学' : '教师') + (a?.ordinal || '待分配'),
    ...(user.role === 'student' ? { student_no: a ? String(a.ordinal) : null } : {}),
    ...(user.class_id ? { class_name: classAlias ? '班级' + classAlias.ordinal : '班级待分配' } : {})
  };
  delete visible.username;
  return { ...visible, identity_anonymized: true };
}

function alias(db, userId) {
  if (!enabled(db)) return null;
  return db.prepare('SELECT user_id,role,ordinal FROM presentation_user_aliases WHERE user_id=?').get(Number(userId)) || null;
}

function nextOrdinal(db, role) {
  const row = db.prepare('SELECT COALESCE(MAX(ordinal),0)+1 AS next FROM presentation_user_aliases WHERE role=?').get(role);
  return Number(row?.next || 1);
}

function nextClassOrdinal(db) {
  const row = db.prepare('SELECT COALESCE(MAX(ordinal),0)+1 AS next FROM presentation_class_aliases').get();
  return Number(row?.next || 1);
}

module.exports = { enabled, profile, visibleProfile, alias, nextOrdinal, nextClassOrdinal };
