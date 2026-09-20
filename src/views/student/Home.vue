<template>
  <div class="space-y-6 max-w-7xl">
    <!-- 欢迎横幅 -->
    <div class="relative overflow-hidden rounded-4xl p-8 text-white shadow-lift"
      style="background:linear-gradient(120deg,#0a4740,#078775 55%,#0891b2)">
      <div class="absolute -right-10 -top-14 w-64 h-64 rounded-full bg-white/10"></div>
      <div class="absolute right-24 bottom-[-60px] w-36 h-36 rounded-full bg-white/10"></div>
      <div class="relative">
        <div class="text-brand-100 text-sm">{{ greet }}，{{ auth.user?.name }}</div>
        <h2 class="text-3xl font-bold mt-2">准备好探索霍尔效应了吗？</h2>
        <p class="text-white/75 mt-2 max-w-lg">从仿真实验开始，完成四方向测量，再到数据工坊拟合分析，系统将记录你的每一步成长。</p>
        <div class="flex gap-3 mt-6">
          <router-link to="/sim/lab" class="btn bg-white text-brand-800 font-semibold hover:-translate-y-0.5"><Icon name="play" :size="17"/> 进入仿真实验</router-link>
          <router-link to="/resources/projects" class="btn bg-white/15 text-white"><Icon name="folder" :size="17"/> 探究课题</router-link>
        </div>
      </div>
    </div>

    <!-- 数据概览 -->
    <div class="grid grid-cols-2 lg:grid-cols-4 gap-4">
      <div v-for="s in stats" :key="s.label" class="card-hover p-5 flex items-center gap-4">
        <div class="w-12 h-12 rounded-2xl flex items-center justify-center" :style="{background:s.bg,color:s.color}"><Icon :name="s.icon" /></div>
        <div>
          <div class="text-2xl font-bold text-ink-900">{{ s.value }}</div>
          <div class="text-xs text-ink-400">{{ s.label }}</div>
        </div>
      </div>
    </div>

    <!-- 快捷入口 -->
    <div>
      <h3 class="font-bold text-ink-900 mb-3 flex items-center gap-2"><Icon name="spark" class="text-brand-500"/> 学习模块</h3>
      <div class="grid md:grid-cols-3 gap-4">
        <router-link v-for="q in quick" :key="q.to" :to="q.to" class="card-hover p-6 group">
          <div class="w-12 h-12 rounded-2xl flex items-center justify-center mb-4" :style="{background:q.bg}"><Icon :name="q.icon" :size="22" class="text-white"/></div>
          <div class="font-bold text-ink-900">{{ q.title }}</div>
          <div class="text-sm text-ink-400 mt-1 leading-6">{{ q.desc }}</div>
          <div class="mt-4 text-brand-600 text-sm flex items-center gap-1 group-hover:gap-2 transition-all">进入学习 <Icon name="chevronR" :size="15"/></div>
        </router-link>
      </div>
    </div>

    <!-- 最近会话 + 推荐 -->
    <div class="grid lg:grid-cols-3 gap-4">
      <div class="card p-6 lg:col-span-2">
        <div class="flex items-center mb-4">
          <h3 class="font-bold text-ink-900">我的实验记录</h3>
          <router-link to="/workshop/cleaning" class="ml-auto text-sm text-brand-600">数据清洗台 →</router-link>
        </div>
        <div class="space-y-3">
          <template v-if="loading"><div v-for="i in 3" :key="i" class="skeleton h-[60px]"></div></template>
          <template v-else>
          <div v-for="s in sessions.slice(0,4)" :key="s.id" class="flex items-center gap-4 p-3 rounded-2xl hover:bg-ink-50">
            <div :class="['w-10 h-10 rounded-2xl flex items-center justify-center', s.finished?'bg-brand-50 text-brand-600':'bg-amber-50 text-amber-600']">
              <Icon :name="s.finished?'check':'clock'"/>
            </div>
            <div class="text-sm">
              <div class="font-medium text-ink-800">实验会话 #{{ s.id }} · {{ s.case_id }}</div>
              <div class="text-xs text-ink-400">{{ s.started_at }} · 完整组 {{ s.points }}</div>
            </div>
            <span :class="['ml-auto chip-gray', s.finished?'!bg-brand-50 !text-brand-700':'']">{{ s.finished?'已完成':'进行中' }}</span>
          </div>
          <div v-if="!sessions.length" class="text-center text-sm text-ink-400 py-8">还没有实验记录，点击上方按钮开始吧</div>
          </template>
        </div>
      </div>

      <div class="card p-6">
        <h3 class="font-bold text-ink-900 mb-4">推荐阅读</h3>
        <div class="space-y-3">
          <template v-if="loading"><div v-for="i in 3" :key="i" class="skeleton h-12"></div></template>
          <template v-else>
          <router-link v-for="c in recCases" :key="c.id" :to="'/resources/cases'" class="flex items-center gap-3 group">
            <div class="w-10 h-10 rounded-2xl flex items-center justify-center text-white text-lg" :style="{background:c.color}">{{ c.cover }}</div>
            <div class="text-sm font-medium text-ink-700 group-hover:text-brand-700 leading-5">{{ c.title }}</div>
          </router-link>
          </template>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue';
import Icon from '../../components/Icon.vue';
import { useAuth } from '../../stores/auth';
import { api } from '../../api';
const auth = useAuth();
const sessions = ref([]), recCases = ref([]), loading=ref(true);
const greet = computed(()=>{const h=new Date().getHours();return h<11?'早上好':h<14?'中午好':h<18?'下午好':'晚上好';});

const stats = ref([
  { label:'完成实验会话', value:0, icon:'flask', bg:'#e6faf5', color:'#078775' },
  { label:'自测最佳成绩', value:'—', icon:'award', bg:'#eaf2ff', color:'#2563eb' },
  { label:'课题提交', value:0, icon:'folder', bg:'#fff4e6', color:'#ea580c' },
  { label:'累计异常', value:0, icon:'alert', bg:'#ffe9ee', color:'#e11d48' }
]);
const quick = [
  { to:'/resources/cases', title:'教学资源中心', desc:'真实应用案例、思政素材、自测题与探究课题', icon:'book', bg:'linear-gradient(135deg,#14b8a6,#0d9488)' },
  { to:'/workshop/analysis', title:'数据处理工坊', desc:'数据清洗、不确定度评定与多元拟合分析', icon:'chart', bg:'linear-gradient(135deg,#3b82f6,#2563eb)' },
  { to:'/peer/works', title:'同伴互评', desc:'发布作品、依据Rubric量表互评与反馈申诉', icon:'users', bg:'linear-gradient(135deg,#f59e0b,#ea580c)' }
];

onMounted(async ()=>{
 try{
  sessions.value = await api('/sim/sessions');
  const brief = await api('/workshop/sessions-brief');
  const attempts = await api('/resources/attempts');
  const mySubs = await api('/resources/my-submissions');
  const cases = await api('/resources/cases');
  recCases.value = cases.slice(0,4);
  stats.value[0].value = sessions.value.filter(s=>s.finished).length;
  stats.value[1].value = attempts.length ? Math.round(Math.max(...attempts.map(a=>a.score*100/a.total))) : '—';
  stats.value[2].value = mySubs.length;
  stats.value[3].value = brief.reduce((a,b)=>a+b.errors,0);
 }finally{loading.value=false;}
});
</script>