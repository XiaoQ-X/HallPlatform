<template>
  <div class="max-w-5xl space-y-5" v-if="data">
    <!-- 学生信息 -->
    <div class="card p-5">
      <div class="flex flex-wrap items-center gap-3">
        <router-link to="/teacher/side-effects" class="btn-ghost text-xs"><Icon name="refresh" :size="14" class="rotate-180" /> 返回列表</router-link>
        <div class="w-12 h-12 rounded-2xl bg-brand-50 flex items-center justify-center text-2xl">{{ data.student.avatar }}</div>
        <div>
          <div class="font-bold text-ink-900 text-lg">{{ data.student.name }}</div>
          <div class="text-xs text-ink-400">{{ data.student.student_no||'无学号' }} · {{ data.student.class_name||'未分班' }}</div>
        </div>
        <span class="ml-auto text-xs rounded-full px-3 py-1.5 font-medium" :class="STATUS[data.status].cls">{{ STATUS[data.status].label }}</span>
      </div>
    </div>

    <!-- 未开始空状态 -->
    <div v-if="!data.sessions.length&&!data.records.length" class="card p-10 text-center">
      <div class="text-5xl mb-3">⏳</div>
      <div class="font-bold text-ink-800 mb-1">该学生尚未开始实验</div>
      <p class="text-sm text-ink-400 mb-5">没有仿真会话，也没有提交记录。可向其推送实验任务。</p>
      <router-link to="/teacher/pushes" class="btn-primary inline-flex"><Icon name="send" :size="16" /> 推送实验任务</router-link>
    </div>

    <!-- 评分卡片 -->
    <div class="card p-6" v-if="data.sessions.length||data.records.length">
      <div class="flex items-center gap-2 mb-1">
        <Icon name="award" class="text-brand-600" :size="20" />
        <h2 class="font-bold text-ink-900">实验评分</h2>
        <span v-if="data.grades.length" class="chip-gray ml-auto">已评 {{ data.grades.length }} 次</span>
      </div>
      <p class="text-xs text-ink-400 mb-4">系统不会自动给出最终成绩；请按维度评定，确认后提交。修改评分将再次记录并写入审计。</p>

      <div class="grid sm:grid-cols-2 gap-x-6 gap-y-4">
        <div v-for="d in DIMS" :key="d[0]">
          <div class="flex justify-between text-sm mb-1">
            <span class="text-ink-700">{{ d[1] }}</span>
            <b class="text-brand-700">{{ form[d[0]]===null?'未评':form[d[0]]+' / 10' }}</b>
          </div>
          <input type="range" min="0" max="10" step="0.5" :value="form[d[0]]??0" class="w-full accent-brand-600"
            @input="form[d[0]]=Number($event.target.value)" :aria-label="d[1]">
        </div>
      </div>

      <div class="grid sm:grid-cols-2 gap-4 mt-5">
        <label class="text-xs text-ink-500">评语（可选）
          <textarea v-model="form.comment" rows="3" class="input mt-1" placeholder="对学生实验过程与结论的反馈"></textarea>
        </label>
        <div class="rounded-2xl bg-ink-50 p-4">
          <div class="text-xs text-ink-400 mb-2">总分（百分制）</div>
          <div class="text-sm text-ink-600 mb-2">维度折算建议：<b class="text-brand-700">{{ suggested===null?'需评完全部维度':Math.round(suggested) }}</b></div>
          <input type="number" min="0" max="100" v-model="form.total" class="input" placeholder="留空则使用维度折算">
        </div>
      </div>

      <div class="flex flex-wrap items-center gap-3 mt-5">
        <button class="btn-primary" :disabled="saveState==='loading'" @click="submitGrade">
          <Icon v-if="saveState==='loading'" name="refresh" :size="16" class="animate-spin" />
          {{ saveState==='loading'?'提交中…':(data.grades.length?'提交修改评分':'提交评分') }}
        </button>
        <span v-if="saveState==='success'" class="status-pill on-emerald"><Icon name="check" :size="15"/> {{ saveMsg }}</span>
        <span v-if="saveState==='error'" role="alert" class="status-pill !bg-rose-50 !text-rose-600"><Icon name="alert" :size="15"/> {{ saveMsg }}</span>
      </div>
    </div>

    <!-- 学生提交的实验结论 -->
    <div class="card p-6" v-if="data.records.length">
      <h2 class="font-bold text-ink-900 mb-3 flex items-center gap-2"><Icon name="chat" class="text-brand-600" :size="19" /> 学生提交记录与结论</h2>
      <details v-for="(r,i) in data.records" :key="r.id" :open="i===0" class="rounded-2xl border border-ink-100 p-4 mb-2">
        <summary class="cursor-pointer text-sm font-medium text-ink-800">{{ r.title }} <span class="text-xs text-ink-400">· {{ r.created_at }}</span></summary>
        <div class="mt-3 space-y-3 text-sm">
          <div>
            <div class="text-xs text-ink-400 mb-1">实验结论</div>
            <div class="rounded-xl bg-brand-50/60 p-3 text-ink-700 whitespace-pre-wrap leading-7">{{ r.conclusion||'（学生未填写结论）' }}</div>
          </div>
          <div class="grid grid-cols-4 gap-2 text-center">
            <div class="rounded-xl bg-ink-50 py-2"><div class="text-[10px] text-ink-400">理想VH</div><b class="text-[12px] text-brand-700">{{ f(r.result.idealVH_mV) }}</b></div>
            <div class="rounded-xl bg-ink-50 py-2"><div class="text-[10px] text-ink-400">V0</div><b class="text-[12px] text-amber-600">{{ f(r.params?r.params.r0*r.params.I_mA:null) }}</b></div>
            <div class="rounded-xl bg-ink-50 py-2"><div class="text-[10px] text-ink-400">VE</div><b class="text-[12px] text-rose-600">{{ f(r.result.residualVE_mV) }}</b></div>
            <div class="rounded-xl bg-ink-50 py-2"><div class="text-[10px] text-ink-400">修正值</div><b class="text-[12px] text-sky-700">{{ f(r.result.correctedHall_mV) }}</b></div>
          </div>
        </div>
      </details>
    </div>

    <!-- Unity 会话详情 -->
    <div class="card p-6" v-if="data.sessions.length">
      <h2 class="font-bold text-ink-900 mb-3 flex items-center gap-2"><Icon name="flask" class="text-brand-600" :size="19" /> 仿真工作台会话</h2>
      <details v-for="(s,i) in data.sessions" :key="s.id" :open="i===0" class="rounded-2xl border border-ink-100 p-4 mb-2">
        <summary class="cursor-pointer text-sm font-medium text-ink-800">
          会话 #{{ s.id }} · {{ s.finished?'已完成':'进行中' }}
          <span class="text-xs text-ink-400">{{ s.started_at }}</span>
        </summary>

        <div class="mt-3 text-sm space-y-3">
          <!-- 四方向测量 -->
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-2 text-center">
            <div v-for="m in s.measurements" :key="m.id" class="rounded-xl bg-ink-50 py-2">
              <div class="text-[10px] text-ink-400">槽位{{ m.slot }}（{{ m.is_dir>0?'+':'−' }}，{{ m.im_dir>0?'+':'−' }}）</div>
              <b class="text-[12px] text-sky-700">{{ f(m.raw_mV) }}</b>
              <div class="text-[9px] text-ink-400">V0 {{ f(m.payload?.V0_mV) }} · VE {{ f(m.payload?.VE_mV) }} · VN {{ f(m.payload?.VN_mV) }} · VRL {{ f(m.payload?.VRL_mV) }}</div>
            </div>
          </div>

          <!-- report 汇总 -->
          <template v-if="s.state.report">
            <div class="grid grid-cols-3 gap-2 text-center">
              <div class="rounded-xl bg-rose-50 py-2"><div class="text-[10px] text-ink-400">修正前 V1</div><b class="text-[12px] text-rose-600">{{ f(s.state.report.beforeCorrection_mV) }}</b></div>
              <div class="rounded-xl bg-amber-50 py-2"><div class="text-[10px] text-ink-400">修正值</div><b class="text-[12px] text-amber-600">{{ f(s.state.report.correctedHall_mV) }}</b></div>
              <div class="rounded-xl bg-brand-50 py-2"><div class="text-[10px] text-ink-400">修正后理想VH</div><b class="text-[12px] text-brand-700">{{ f(s.state.report.afterCorrection_mV) }}</b></div>
            </div>
            <div class="text-xs text-ink-400">完成时间 {{ s.state.report.completedAt }} · 关键操作 {{ s.state.report.operations?.length||0 }} 步</div>
            <details class="text-xs">
              <summary class="cursor-pointer text-brand-700">查看参数快照与关键操作</summary>
              <div class="rounded-xl bg-ink-50 p-3 mt-2 text-ink-600 leading-6">
                <div>参数：IS {{ s.state.report.params?.IS_mA }} mA · IM {{ s.state.report.params?.IM_A }} A · B {{ s.state.report.params?.B_T }} T · r0 {{ s.state.report.params?.r0 }} Ω · kE {{ s.state.report.params?.kE }} · kN {{ s.state.report.params?.kN_mV_T ?? 0 }} mV/T · kRL {{ s.state.report.params?.kRL_mV_T ?? 0 }} mV/T</div>
                <ul class="list-disc pl-4 mt-1">
                  <li v-for="(o,j) in s.state.report.operations" :key="j">{{ o.time }} · {{ o.text }}</li>
                </ul>
              </div>
            </details>
          </template>

          <div v-if="s.abnormals.length" class="text-xs text-rose-600">
            异常 {{ s.abnormals.length }} 次：<span v-for="a in s.abnormals" :key="a.id" class="mr-2">{{ a.code }}</span>
          </div>
        </div>
      </details>
    </div>

    <!-- 评分历史 -->
    <div class="card p-6" v-if="data.grades.length">
      <h2 class="font-bold text-ink-900 mb-3">评分历史</h2>
      <div v-for="g in data.grades" :key="g.id" class="flex flex-wrap items-center gap-2 py-2 border-b border-ink-100 text-sm">
        <span class="chip-gray">第 {{ g.id }} 次</span>
        <b class="text-amber-600">{{ Math.round(g.total) }} 分</b>
        <span class="text-xs text-ink-400">{{ g.comment||'无评语' }}</span>
        <span class="text-xs text-ink-400 ml-auto">{{ g.created_at }}</span>
      </div>
    </div>
  </div>
  <!-- 加载骨架 -->
  <div v-else-if="loadError" class="max-w-5xl card p-10 text-center">
    <div class="text-4xl mb-3">⚠️</div>
    <div class="font-bold text-ink-800 mb-2">学生实验记录加载失败</div>
    <p class="text-sm text-rose-600 mb-5">{{ loadError }}</p>
    <button class="btn-primary" @click="load">重新加载</button>
  </div>
  <div v-else class="max-w-5xl space-y-5 animate-pulse">
    <div class="h-14 rounded-3xl bg-white/70"></div>
    <div class="h-72 rounded-3xl bg-white/70"></div>
    <div class="h-56 rounded-3xl bg-white/70"></div>
    <p class="text-sm text-ink-400 text-center pt-1">正在加载学生实验记录…</p>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue';
import { useRoute } from 'vue-router';
import Icon from '../../components/Icon.vue';
import { api } from '../../api';

const route = useRoute();
const data = ref(null);
const loadError = ref('');
const STATUS = {
  not_started: { label: '未开始', cls: 'bg-ink-100 text-ink-500' },
  in_progress: { label: '进行中', cls: 'bg-sky-100 text-sky-700' },
  completed: { label: '已完成', cls: 'bg-emerald-100 text-emerald-700' },
  submitted: { label: '已提交', cls: 'bg-brand-100 text-brand-700' },
  graded: { label: '已评分', cls: 'bg-amber-100 text-amber-700' }
};
const DIMS = [
  ['dim_params', '参数设置'],
  ['dim_steps', '测量步骤'],
  ['dim_v0', '是否识别 V0'],
  ['dim_ve', '是否识别 VE'],
  ['dim_reversal', '是否完成换向修正'],
  ['dim_explain', '结果解释']
];
const form = reactive({ dim_params:null, dim_steps:null, dim_v0:null, dim_ve:null, dim_reversal:null, dim_explain:null, comment:'', total:'' });
const saveState = ref('idle'), saveMsg = ref('');

const f = v => Number.isFinite(v) ? Number(v).toFixed(2) : '—';
const suggested = computed(()=>{
  const vals = DIMS.map(d=>form[d[0]]);
  if(vals.some(v=>v===null))return null;
  return vals.reduce((a,b)=>a+b,0)/60*100;
});

function prefillFromLatest(){
  const g = data.value.grades[0];
  if(!g)return;
  for(const [k] of DIMS)form[k]=g[k];
  form.comment=g.comment||'';
}
async function load(){
  loadError.value='';
  try { data.value = await api('/teacher/side-effects/student/'+route.params.studentId); prefillFromLatest(); }
  catch(e){ data.value=null; loadError.value=e.message||'学生实验记录加载失败'; }
}
async function submitGrade(){
  const scores={};let all=true;
  for(const [k] of DIMS){scores[k]=form[k];if(form[k]===null)all=false;}
  let total=null;
  if(form.total!==''&&Number.isFinite(Number(form.total)))total=Number(form.total);
  else if(all)total=DIMS.reduce((a,[k])=>a+form[k],0)/60*100;
  else { saveState.value='error';saveMsg.value='请完成全部维度评分，或直接填写总分';return; }
  saveState.value='loading';saveMsg.value='';
  try{
    const finished = data.value.sessions.find(s=>s.finished);
    await api('/teacher/side-effects/grade',{method:'POST',body:{
      student_id:data.value.student.id,scores,comment:form.comment,total,
      session_id:finished?.id||null,record_id:data.value.records[0]?.id||null}});
    saveState.value='success';saveMsg.value='评分已保存并写入审计';
    await load();
  }catch(e){ saveState.value='error';saveMsg.value='保存失败：'+e.message; }
}

onMounted(load);
</script>
