// Formula/content alignment migration.  The Unity magnetic model uses a
// Langevin saturation curve, so VH-IM is only approximately linear before
// saturation.  Keep the project guidance consistent with the implemented
// model for both existing and freshly provisioned databases.
module.exports = db => {
  if (db.prepare('SELECT 1 FROM migrations WHERE version=142').get()) return;
  db.exec('BEGIN IMMEDIATE');
  try {
    const project = db.prepare("SELECT id, title, subtitle, guide, tasks FROM projects WHERE title='霍尔电压定量关系探究' LIMIT 1").get();
    if (project) {
      let tasks = [];
      try { tasks = JSON.parse(project.tasks || '[]'); } catch {}
      tasks = tasks.map(task => task && typeof task.t === 'string' && task.t.includes('拟合两条直线')
        ? { ...task, t: '在数据工坊拟合模型，报告斜率、R²，并说明 VH-IM 线性近似的适用范围' }
        : task);
      const subtitle = '探究 V_H 与 I_S、I_M 的关系及线性近似适用范围';
      const guide = String(project.guide || '').replace(
        '用数据工坊完成线性拟合。',
        '在未饱和工作区用数据工坊完成线性拟合，并观察接近饱和时的偏离。'
      );
      db.prepare('UPDATE projects SET subtitle=?,guide=?,tasks=? WHERE id=?')
        .run(subtitle, guide, JSON.stringify(tasks), project.id);
    }
    const caseRow = db.prepare("SELECT id, content FROM cases WHERE title LIKE '电动车的%霍尔电流传感器%' LIMIT 1").get();
    if (caseRow && caseRow.content) {
      const content = caseRow.content
        .replace('固定励磁电流、改变工作电流', '固定工作电流、扫描励磁电流')
        .replace('观察霍尔电压随电流线性变化', '在未饱和区观察霍尔电压的近似线性变化，并记录接近饱和时的偏离');
      db.prepare('UPDATE cases SET content=? WHERE id=?').run(content, caseRow.id);
    }
    db.prepare('INSERT INTO migrations(version) VALUES(142)').run();
    db.exec('COMMIT');
  } catch (e) {
    db.exec('ROLLBACK');
    throw e;
  }
};
