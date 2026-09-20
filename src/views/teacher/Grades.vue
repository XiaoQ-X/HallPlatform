<template>
  <div class="max-w-7xl space-y-5">
    <div class="card p-4 flex flex-wrap items-center gap-3">
      <div class="text-sm text-ink-500">权重：仿真30% · 自测25% · 课题30% · 互评15%</div>
      <button class="btn-soft ml-auto" @click="exportCsv"><Icon name="download":size="16"/> 导出成绩CSV</button>
      <button class="btn-primary" @click="publish">发布成绩快照</button>
    </div>

    <div class="card p-6 overflow-x-auto">
      <table class="w-full text-sm min-w-800">
        <thead><tr class="text-xs text-ink-400">
          <th class="text-left py-3 font-medium">学号</th><th class="text-left font-medium">姓名</th>
          <th class="font-medium">仿真实验<br/>(30%)</th><th class="font-medium">自测题<br/>(25%)</th>
          <th class="font-medium">课题包<br/>(30%)</th><th class="font-medium">同伴互评<br/>(15%)</th>
          <th class="font-medium">加权总分</th><th class="font-medium">等级</th><th class="font-medium">调整</th></tr></thead>
        <tbody>
          <tr v-for="g in grades" :key="g.id" class="border-t border-ink-50 text-center hover:bg-ink-50/60">
            <td class="text-left py-3 text-ink-400">{{ g.student_no }}</td>
            <td class="text-left font-medium text-ink-800">{{ g.name }}<details v-if="g.pending.length" class="text-xs text-amber-700"><summary>待完成 {{g.pending.length}} 项</summary><p v-for="p in g.pending">{{p}}</p></details></td>
            <td v-for="k in ['sim','quiz','project','peer']" :key="k">
              <span :title="g.comp[k].overridden?'已手动调整':''"
                :class="g.comp[k].overridden?'text-amber-600 font-semibold border-b border-dotted border-amber-400':''">{{ cell(g.comp[k].score) }}</span></td>
            <td><b :class="gradeColor(g.total)">{{ g.total }}</b></td>
            <td><span :class="['chip text-[11px]',gradeChip(g.total)]">{{ gradeLetter(g.total) }}</span></td>
            <td><button class="text-brand-600 text-xs" @click="openAdj(g)">调整</button></td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- 成绩调整模态 -->
    <div v-if="adj" class="modal-mask" @click.self="adj=null">
      <div class="modal p-6">
        <h3 class="font-bold text-lg mb-1">手动调整成绩</h3>
        <p class="text-xs text-ink-400 mb-4">学生：{{ adj.name }}（调整后覆盖系统计算分）</p>
        <label class="text-xs text-ink-400">选择组件</label>
        <select v-model="adjComp" @change="adjScore=adj.comp[adjComp].score" class="input mb-3">
          <option value="sim">仿真实验</option><option value="quiz">自测题</option>
          <option value="project">课题包</option><option value="peer">同伴互评</option></select>
        <label class="text-xs text-ink-400">调整为（0-100）</label>
        <input type="number" min="0" max="100" v-model.number="adjScore" class="input mb-4"/>
        <label>调整依据<textarea v-model="reason" class="input mb-3"/></label>
        <div class="flex gap-3">
          <button class="btn-ghost flex-1" @click="clearAdj">清除调整</button>
          <button class="btn-primary flex-1" @click="saveAdj">应用</button></div>
      </div>
    </div>

    <!-- 班级均分 -->
    <div class="grid grid-cols-2 lg:grid-cols-5 gap-4">
      <div class="card p-5 text-center"><div class="text-xs text-ink-400">班级总均分</div><b class="text-2xl gradient-text">{{ avg.total }}</b></div>
      <div v-for="k in ['sim','quiz','project','peer']" :key="k" class="card p-5 text-center">
        <div class="text-xs text-ink-400">{{ name[k] }}均分</div><b class="text-xl text-ink-800">{{ avg[k] }}</b></div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import Icon from '../../components/Icon.vue';
import { api } from '../../api';
const grades=ref([]);
const adj=ref(null), adjComp=ref('sim'), adjScore=ref(null);
const reason=ref('');
async function publish(){const reason=prompt('本次发布说明（待评成果须先评阅，缺交项目按当前政策计分）');if(reason){const r=await api('/teacher/grades/publish',{method:'POST',body:{reason}});alert('已发布 '+r.count+' 名学生的成绩快照');}}
function openAdj(g){ adj.value=g; adjComp.value='sim'; adjScore.value=g.comp.sim.score; }
async function saveAdj(){
  await api('/teacher/grades/override',{method:'POST',
    body:{student_id:adj.value.id,component:adjComp.value,score:adjScore.value,reason:reason.value}});
  adj.value=null; grades.value=await api('/teacher/grades');
}
async function clearAdj(){
  await api('/teacher/grades/override',{method:'DELETE',
    body:{student_id:adj.value.id,component:adjComp.value,reason:reason.value}});
  adj.value=null; grades.value=await api('/teacher/grades');
}
const name={sim:'仿真',quiz:'自测',project:'课题',peer:'互评'};
const cell = v => v || 0;
function gradeColor(v){ return v>=90?'text-emerald-600':v>=75?'text-brand-700':v>=60?'text-amber-600':'text-rose-500'; }
function gradeChip(v){ return v>=90?'!bg-emerald-100 text-emerald-800':v>=75?'!bg-brand-100 text-brand-800':v>=60?'!bg-amber-100 text-amber-800':'!bg-rose-100 text-rose-700'; }
function gradeLetter(v){ return v>=90?'优秀':v>=80?'良好':v>=70?'中等':v>=60?'及格':'不及格'; }
const avg=computed(()=>{
  const a={total:0,sim:0,quiz:0,project:0,peer:0};
  if(!grades.value.length)return a;
  grades.value.forEach(g=>{a.total+=g.total;
    ['sim','quiz','project','peer'].forEach(k=>a[k]+=g.comp[k].score);});
  const n=grades.value.length;
  Object.keys(a).forEach(k=>a[k]=Math.round(a[k]/n*10)/10);
  return a;
});
function exportCsv(){
  const csv=value=>{let s=String(value??'');if(/^[\s]*[=+@-]/.test(s))s="'"+s;return '"'+s.replace(/"/g,'""')+'"';};
  const head=['学号','姓名','仿真','自测','课题','互评','总分','等级'];
  const lines=grades.value.map(g=>[g.student_no,g.name,g.comp.sim.score,g.comp.quiz.score,
    g.comp.project.score,g.comp.peer.score,g.total,gradeLetter(g.total)].map(csv).join(','));
  const blob=new Blob(['\ufeff'+[head.join(','),...lines].join('\n')],{type:'text/csv'});
  const a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download='霍尔效应课程成绩汇总.csv';a.click();setTimeout(()=>URL.revokeObjectURL(a.href),1000);
}
onMounted(async()=>{ grades.value=await api('/teacher/grades'); });
</script>
