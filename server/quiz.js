const normalize = value => String(value ?? '').trim().replace(/\s+/g,'').toLowerCase();
function accepted(answer) {try {const a=JSON.parse(answer);if(Array.isArray(a))return a;}catch{}return [answer];}
function gradeAnswers(questions, answers) {
  let score=0,total=0;
  const detail=questions.map(q=>{
    const max=Number(q.score);
    if(!Number.isFinite(max)||max<=0)throw Object.assign(new Error('题目分值无效'),{status:422});
    total+=max;const answer=answers[q.id];let correct=false;
    if(q.type==='multiple')correct=Array.isArray(answer)&&[...new Set(answer)].sort().join('')===String(q.answer).split('').sort().join('');
    else if(q.type!=='essay')correct=accepted(q.answer).some(x=>normalize(x)===normalize(answer));
    const earned=correct?max:0;score+=earned;
    return {id:q.id,correct:q.type==='essay'?null:correct,score:earned,max,pending:q.type==='essay',stem:q.stem,answer:q.answer,analysis:q.analysis};
  });return {score,total,detail,status:detail.some(x=>x.pending)?'pending':'graded'};
}
module.exports={gradeAnswers};
