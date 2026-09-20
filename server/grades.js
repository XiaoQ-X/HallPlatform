const {db,parse}=require('./common');
const round=x=>Math.round(x*10)/10;
function forStudent(student){
  const pending=[];
  const owner=db.prepare('SELECT c.teacher_id FROM users u JOIN classes c ON c.id=u.class_id WHERE u.id=?').get(student.id)?.teacher_id||0;
  const session=db.prepare('SELECT quality_score FROM sim_sessions WHERE student_id=? AND demo=0 AND finished=1 AND verified=1 ORDER BY id DESC LIMIT 1').get(student.id);
  if(!session)pending.push('仿真实验待教师核验');
  const quizzes=db.prepare('SELECT id FROM quizzes WHERE published=1 AND archived=0 AND owner_id=?').all(owner);
  const qScores=quizzes.map(q=>{const score=db.prepare("SELECT MAX(score*100.0/total) score FROM quiz_attempts WHERE student_id=? AND quiz_id=? AND status='graded' AND total>0").get(student.id,q.id).score;if(score==null)pending.push('自测#'+q.id+'未完成/待评');return score||0;});
  const projects=db.prepare('SELECT id FROM projects WHERE published=1 AND archived=0 AND owner_id=?').all(owner);
  const pScores=projects.map(p=>{const s=db.prepare('SELECT grade FROM project_submissions WHERE student_id=? AND project_id=?').get(student.id,p.id);if(s?.grade==null)pending.push('课题#'+p.id+'未提交/待评');return s?.grade||0;});
  const reviews=db.prepare('SELECT r.* FROM reviews r JOIN review_items i ON i.id=r.item_id WHERE i.student_id=? AND r.voided=0').all(student.id);
  const received=[];
  for(const rv of reviews){const dims=parse(rv.snapshot,[]),scores=parse(rv.scores);if(!dims.length||dims.some(d=>typeof scores[d.name]!=='number'||!Number.isFinite(scores[d.name])||scores[d.name]<0||scores[d.name]>d.max)){pending.push('互评#'+rv.id+'历史分数无效，需复核');continue;}received.push(dims.reduce((s,d)=>s+scores[d.name],0)*100/dims.reduce((s,d)=>s+d.max,0));}
  const assigned=db.prepare('SELECT COUNT(*) n FROM review_assignments WHERE reviewer_id=?').get(student.id).n;
  const done=db.prepare('SELECT COUNT(*) n FROM review_assignments a JOIN reviews r ON r.item_id=a.item_id AND r.reviewer_id=a.reviewer_id AND r.voided=0 WHERE a.reviewer_id=?').get(student.id).n;
  if(!received.length||done<assigned)pending.push('互评尚未完成');
  const avg=a=>a.length?a.reduce((s,x)=>s+x,0)/a.length:0;
  const comp={sim:{score:session?.quality_score||0,weight:.3},quiz:{score:avg(qScores),weight:.25},project:{score:avg(pScores),weight:.3},peer:{score:avg(received)*.7+(assigned?done/assigned*100:0)*.3,weight:.15}};
  for(const o of db.prepare('SELECT * FROM grade_overrides WHERE student_id=? ORDER BY id').all(student.id))if(comp[o.component]&&Number.isFinite(o.score)&&o.score>=0&&o.score<=100){comp[o.component].score=o.score;comp[o.component].overridden=true;}
  for(const c of Object.values(comp))c.score=round(c.score);
  return {id:student.id,name:student.name,student_no:student.student_no,class_name:student.class_name,comp,total:round(Object.values(comp).reduce((s,c)=>s+c.score*c.weight,0)),pending,policy:'v1.4：逐卷最高分平均；已发布课题按项平均；互评质量70%+任务完成30%；仿真教师核验'};
}
module.exports={forStudent};
