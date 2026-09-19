<template>
  <div class="max-w-4xl">
    <!-- 试卷列表 -->
    <div v-if="phase==='list'">
      <div class="rounded-4xl p-8 mb-6 text-white shadow-lift relative overflow-hidden"
        style="background:linear-gradient(120deg,#0a4740,#078775 60%,#0284c7)">
        <div class="absolute right-10 top-4 text-7xl opacity-20">📝</div>
        <h2 class="text-2xl font-bold">思考题 / 自测题</h2>
        <p class="text-white/75 mt-2">检验你对霍尔效应原理、公式、仪器与应用的掌握程度。</p>
      </div>
      <div class="grid md:grid-cols-2 gap-5">
        <div v-for="q in quizzes" :key="q.id" class="card-hover p-6">
          <div class="flex items-start">
            <div class="w-12 h-12 rounded-2xl bg-brand-50 text-brand-600 flex items-center justify-center"><Icon name="edit"/></div>
            <span class="ml-auto chip-gray">{{ q.question_ids.length }} 题 · {{ q.time_minutes }}分钟</span>
          </div>
          <div class="font-bold text-ink-900 mt-4">{{ q.title }}</div>
          <div class="text-sm text-ink-400 mt-1 leading-6">{{ q.description }}</div>
          <button class="btn-primary w-full mt-5" @click="start(q)">开始答题</button>
        </div>
      </div>
      <div v-if="attempts.length" class="card p-6 mt-6">
        <h3 class="font-bold text-ink-900 mb-3">我的答题记录</h3>
        <div v-for="a in attempts.slice(0,5)" :key="a.id" class="flex items-center text-sm py-2 border-b border-ink-50 last:border-0">
          <span>{{ a.quiz_title }}</span><span class="ml-auto text-ink-400 text-xs">{{ a.submitted_at }}</span>
          <b class="ml-4 text-brand-700 w-24 text-right">{{ a.score }}/{{ a.total }}</b>
        </div>
      </div>
    </div>

    <!-- 答题页 -->
    <div v-else-if="phase==='take'" class="animate-fadeUp">
      <div class="card p-6 mb-4 flex items-center">
        <b class="text-ink-900">{{ quiz.title }}</b>
        <span class="ml-auto chip flex items-center gap-1.5"><Icon name="clock":size="14"/> {{ fmt(remain) }}</span>
      </div>
      <div class="space-y-4">
        <div v-for="(q,i) in quiz.questions" :key="q.id" class="card p-6">
          <div class="flex gap-2 font-medium text-ink-900">
            <span class="text-brand-600">{{ i+1 }}.</span>
            <span class="leading-7">{{ q.stem }} <span class="text-xs text-ink-400">（{{ q.score }}分）</span></span>
          </div>
          <div class="mt-4 space-y-2.5 pl-1">
            <!-- 选择 -->
            <template v-if="q.type==='single'">
              <button v-for="(o,k) in q.options" :key="k" @click="ans[q.id]=letter(k)"
                :class="['w-full text-left px-4 py-3 rounded-2xl text-sm border transition flex items-center gap-3',
                  ans[q.id]===letter(k)?'border-brand-400 bg-brand-50 text-brand-800':'border-ink-100 hover:bg-ink-50']">
                <span :class="['w-6 h-6 rounded-full border flex items-center justify-center text-xs',ans[q.id]===letter(k)?'bg-brand-600 text-white border-brand-600':'border-ink-300']">{{ letter(k) }}</span>{{ o }}
              </button>
            </template>
            <template v-else-if="q.type==='multiple'">
              <button v-for="(o,k) in q.options" :key="k" @click="toggleMulti(q.id,letter(k))"
                :class="['w-full text-left px-4 py-3 rounded-2xl text-sm border transition flex items-center gap-3',
                  (ans[q.id]||[]).includes(letter(k))?'border-brand-400 bg-brand-50 text-brand-800':'border-ink-100 hover:bg-ink-50']">
                <span :class="['w-6 h-6 rounded-lg border flex items-center justify-center text-xs',(ans[q.id]||[]).includes(letter(k))?'bg-brand-600 text-white border-brand-600':'border-ink-300']">{{ (ans[q.id]||[]).includes(letter(k))?'✓':'' }}</span>{{ o }}
              </button>
            </template>
            <template v-else-if="q.type==='judge'">
              <div class="flex gap-3">
                <button @click="ans[q.id]='对'" :class="['flex-1 py-3 rounded-2xl border text-sm',ans[q.id]=='对'?'border-brand-400 bg-brand-50 text-brand-800':'border-ink-100']">✓ 正确</button>
                <button @click="ans[q.id]='错'" :class="['flex-1 py-3 rounded-2xl border text-sm',ans[q.id]=='错'?'border-rose-400 bg-rose-50 text-rose-700':'border-ink-100']">✗ 错误</button>
              </div>
            </template>
            <template v-else>
              <input v-model="ans[q.id]" class="input" :placeholder="q.type==='essay'?'请输入你的论述…':'请输入答案'"/>
            </template>
          </div>
        </div>
      </div>
      <div class="flex gap-3 mt-5">
        <button class="btn-ghost" @click="phase='list'">暂不提交</button>
        <button class="btn-primary flex-1" @click="submit">提交答卷</button>
      </div>
    </div>

    <!-- 结果页 -->
    <div v-else class="animate-fadeUp">
      <div class="card p-10 text-center">
        <div class="w-24 h-24 rounded-full mx-auto flex items-center justify-center text-white text-3xl shadow-glow"
          :style="{background:result.score/result.total>=0.6?'linear-gradient(135deg,#14b8a6,#0d9488)':'linear-gradient(135deg,#fb7185,#e11d48)'}">
          {{ Math.round(result.score*100/result.total) }}</div>
        <div class="text-sm text-ink-400 mt-3">本次得分（百分制）</div>
        <b class="text-2xl text-ink-900">{{ result.score }} / {{ result.total }} 分</b>
      </div>
      <div class="card p-6 mt-5 space-y-4">
        <h3 class="font-bold text-ink-900">题目解析</h3>
        <div v-for="(d,i) in result.detail" :key="d.id" class="p-4 rounded-2xl" :class="d.pending?'bg-amber-50':d.correct?'bg-brand-50/60':'bg-rose-50/60'">
          <div class="flex gap-2 text-sm">
            <Icon :name="d.pending?'clock':d.correct?'check':'x'" :size="16"
              :class="d.pending?'text-amber-500':d.correct?'text-brand-600':'text-rose-500'"/>
            <span class="font-medium text-ink-800">{{ qmap[d.id]?.stem }}</span>
          </div>
          <div class="text-xs mt-2 pl-6 space-y-1 text-ink-500">
            <div>你的答案：<b :class="d.correct?'text-brand-700':'text-rose-600'">{{ display(d.id) }}</b></div>
            <div v-if="!d.pending">正确答案：<b class="text-brand-700">{{ qmap[d.id]?.answer }}</b></div>
            <div v-if="qmap[d.id]?.analysis">解析：{{ qmap[d.id].analysis }}</div>
          </div>
        </div>
      </div>
      <button class="btn-primary w-full mt-5" @click="phase='list'">返回试卷列表</button>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onBeforeUnmount } from 'vue';
import Icon from '../../components/Icon.vue';
import { api } from '../../api';
const quizzes = ref([]), attempts = ref([]);
const phase = ref('list');
const quiz = ref({});
const ans = ref({});
const result = ref({});
const qmap = ref({});
let timer=null, remain=ref(0), startedAt=new Date().toISOString();
const letter = k => String.fromCharCode(65+k);
const fmt = s => `${Math.floor(s/60)}:${String(s%60).padStart(2,'0')}`;
function toggleMulti(id,k){ const a=ans.value[id]||[]; const i=a.indexOf(k);
  if(i>=0)a.splice(i,1); else a.push(k); ans.value[id]=a; }
async function start(q){
  quiz.value = await api('/resources/quizzes/'+q.id);
  ans.value={}; phase.value='take';
  remain.value = (q.time_minutes||20)*60;
  clearInterval(timer);
  timer=setInterval(()=>{ remain.value--; if(remain.value<=0){clearInterval(timer);submit();}},1000);
}
async function submit(){
  clearInterval(timer);
  result.value = await api('/resources/quizzes/'+quiz.value.id+'/attempt',
    {method:'POST',body:{answers:ans.value,started_at:startedAt}});
  const allq = await api('/resources/questions');
  allq.forEach(x=>qmap.value[x.id]=x);
  phase.value='result';
  attempts.value = await api('/resources/attempts');
}
function display(id){ const a=ans.value[id]; return Array.isArray(a)?a.sort().join(''):(a??'未作答'); }
onMounted(async()=>{ quizzes.value=await api('/resources/quizzes'); attempts.value=await api('/resources/attempts'); });
onBeforeUnmount(()=>clearInterval(timer));
</script>